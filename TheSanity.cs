using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoMod.RuntimeDetour;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using TheSanity.Content.Skies;
using TheSanity.CostumeTile;

namespace TheSanity
{
	// =========================================================================
	// CLASS UTAMA MOD -- wajib namanya sama kayak nama internal mod
	// (build.txt / .csproj), dan cuma boleh ada SATU class : Mod per mod.
	//
	// "partial" WAJIB di sini -- compiler bakal error CS0260 kalau ga ada,
	// karena ada file LAIN di project ini yang juga declare
	// "partial class TheSanity" (biasanya file generated/split lain yang
	// numpang nambahin member ke class Mod utama). Kalau kamu ga sengaja
	// bikin declaration kedua itu, cari file lain yang punya
	// "partial class TheSanity" dan gabungin isinya ke sini, lalu hapus
	// declaration duplikatnya -- tapi kalau itu memang sengaja (misal
	// generated file), biarin aja, cukup tambahin "partial" di sini.
	// =========================================================================
	public partial class TheSanity : Mod
	{
		// CATATAN PENTING: Main.NewText masih ada (dites lewat build kedua),
		// tapi WorldGen.SpawnHomelessNPC() TERNYATA BENERAN GA ADA LAGI sama
		// sekali di build tModLoader kamu sekarang (2026.6.3.6) -- bukan cuma
		// soal wrapper On_X yang ga ke-generate, method vanilla-nya sendiri
		// kemungkinan udah di-rename/direstruktur. Karena nama barunya ga
		// pasti, method-nya dicari lewat REFLECTION berdasarkan POLA nama
		// (mengandung "Homeless", 0 parameter, static) -- bukan nama persis
		// -- biar tahan kalau nama-nya emang beda dari yang di-assume di sini.
		//
		// Kalau ternyata GA KETEMU sama sekali, fitur retry-nya di-skip aja
		// (log warning) -- BUKAN bikin seluruh mod gagal load.
		private Hook _newTextHookWithForce;
		private Hook _newTextHookNoForce;
		private Hook _spawnHomelessNpcHook;

		public override void Load()
		{
			// Hook 1: sembunyiin pesan "X has arrived" pas spawn-nya di-block
			// sama TownNPCRespawnLockGlobalNPC (npc yang udah pernah mati &
			// bukan lagi di-revive lewat Altar). Pesan arrival vanilla selalu
			// pakai warna RGB (50,125,255) / #327DFF, jadi difilter dari situ
			// -- bukan dari isi teksnya (biar ga kebentur translasi bahasa).
			//
			// CATATAN: beda versi/build tModLoader ternyata beda-beda soal
			// Main.NewText punya parameter "bool force" di belakang atau ngga
			// (build ini TERNYATA cuma punya versi 4-parameter, tanpa force).
			// Daripada nebak satu signature doang dan gagal total kalau
			// tebakannya salah, kita COBA PASANG KE DUA-DUANYA -- overload
			// mana pun yang beneran ada di build kamu bakal kepasang, yang
			// ga ada di-skip diam-diam (log Info doang, bukan Warn, soalnya
			// wajar salah satunya emang ga ada tergantung versi).
			TryHookNewTextWithForce();
			TryHookNewTextNoForce();

			// Hook 2: kalau attempt hari itu ke-block sama lock kita, retry
			// (dibatasi) biar kuota "1 homeless NPC per hari" ga kebuang
			// percuma -- NPC lain yang valid masih kebagian jatah muncul.
			//
			// Dicari lewat POLA NAMA (bukan nameof(WorldGen.SpawnHomelessNPC)
			// langsung, karena member itu ga ada lagi di versi ini) -- ambil
			// method static apapun di WorldGen yang namanya mengandung
			// "Homeless" dan ga punya parameter, sesuai pola method vanilla
			// yang biasa dipake buat nyoba spawn 1 homeless town NPC per hari.
			//
			// Filter JUGA mensyaratkan return type void, karena delegate
			// orig_SpawnHomelessNPC di bawah nganggep method-nya void. Semua
			// kandidat yang ketemu (apapun return type-nya) di-log lewat
			// Logger.Info biar keliatan kalau ada yang cocok namanya tapi
			// return type-nya beda.
			var homelessCandidates = typeof(WorldGen)
				.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
				.Where(m => m.Name.IndexOf("Homeless", StringComparison.OrdinalIgnoreCase) >= 0
					&& m.GetParameters().Length == 0)
				.ToList();

			foreach (MethodInfo candidate in homelessCandidates)
			{
				Logger.Info($"TheSanity: Kandidat method \"Homeless\" ketemu -> {candidate.ReturnType.Name} WorldGen.{candidate.Name}()");
			}

			MethodInfo spawnHomelessMethod = homelessCandidates
				.FirstOrDefault(m => m.ReturnType == typeof(void));

			if (spawnHomelessMethod == null)
			{
				Logger.Warn("TheSanity: Method WorldGen dengan pola nama \"Homeless\" (0 parameter, void) tidak ditemukan -- fitur retry homeless spawn di-skip. Cek log Info di atas buat liat kandidat yang ketemu (kalau ada) dan return type-nya.");
			}
			else
			{
				try
				{
					_spawnHomelessNpcHook = new Hook(
						spawnHomelessMethod,
						typeof(TheSanity).GetMethod(nameof(SpawnHomelessNpcDetour), BindingFlags.NonPublic | BindingFlags.Static));
				}
				catch (Exception ex)
				{
					Logger.Warn($"TheSanity: Gagal masang hook retry homeless spawn ke WorldGen.{spawnHomelessMethod.Name}() (signature-nya ternyata tetep ga cocok) -- fitur ini di-skip. Detail: {ex.Message}");
					_spawnHomelessNpcHook = null;
				}
			}
		}

		private void TryHookNewTextWithForce()
		{
			MethodInfo method = typeof(Main).GetMethod(
				nameof(Main.NewText),
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				types: new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte), typeof(bool) },
				modifiers: null);

			if (method == null)
				return; // wajar ga ketemu di build yang overload-nya cuma 4-parameter

			try
			{
				_newTextHookWithForce = new Hook(
					method,
					typeof(TheSanity).GetMethod(nameof(NewTextDetourWithForce), BindingFlags.NonPublic | BindingFlags.Static));
				Logger.Info("TheSanity: Hook suppress arrival text terpasang lewat overload Main.NewText(string, byte, byte, byte, bool).");
			}
			catch (Exception ex)
			{
				Logger.Warn($"TheSanity: Overload Main.NewText(string, byte, byte, byte, bool) ketemu tapi gagal di-hook. Detail: {ex.Message}");
				_newTextHookWithForce = null;
			}
		}

		private void TryHookNewTextNoForce()
		{
			MethodInfo method = typeof(Main).GetMethod(
				nameof(Main.NewText),
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				types: new[] { typeof(string), typeof(byte), typeof(byte), typeof(byte) },
				modifiers: null);

			if (method == null)
				return; // wajar ga ketemu di build yang overload-nya punya "force"

			try
			{
				_newTextHookNoForce = new Hook(
					method,
					typeof(TheSanity).GetMethod(nameof(NewTextDetourNoForce), BindingFlags.NonPublic | BindingFlags.Static));
				Logger.Info("TheSanity: Hook suppress arrival text terpasang lewat overload Main.NewText(string, byte, byte, byte).");
			}
			catch (Exception ex)
			{
				Logger.Warn($"TheSanity: Overload Main.NewText(string, byte, byte, byte) ketemu tapi gagal di-hook. Detail: {ex.Message}");
				_newTextHookNoForce = null;
			}
		}

		public override void Unload()
		{
			_newTextHookWithForce?.Dispose();
			_newTextHookWithForce = null;

			_newTextHookNoForce?.Dispose();
			_newTextHookNoForce = null;

			_spawnHomelessNpcHook?.Dispose();
			_spawnHomelessNpcHook = null;
		}

		// Dua delegate ini masing-masing HARUS persis cocok sama signature
		// overload Main.NewText yang bersangkutan (orig param di depan, sisanya
		// sama persis kayak method vanilla-nya). Cuma salah satu yang bakal
		// beneran ke-hook tergantung overload mana yang ada di build kamu --
		// lihat TryHookNewTextWithForce/TryHookNewTextNoForce di atas.
		private delegate void orig_NewTextWithForce(string newText, byte R, byte G, byte B, bool force);
		private static void NewTextDetourWithForce(orig_NewTextWithForce orig, string text, byte r, byte g, byte b, bool force)
		{
			if (TownNPCRespawnLockGlobalNPC.SuppressNextArrivalText && r == 50 && g == 125 && b == 255)
			{
				TownNPCRespawnLockGlobalNPC.SuppressNextArrivalText = false;
				return; // teksnya ga jadi ditampilin
			}

			orig(text, r, g, b, force);
		}

		private delegate void orig_NewTextNoForce(string newText, byte R, byte G, byte B);
		private static void NewTextDetourNoForce(orig_NewTextNoForce orig, string text, byte r, byte g, byte b)
		{
			if (TownNPCRespawnLockGlobalNPC.SuppressNextArrivalText && r == 50 && g == 125 && b == 255)
			{
				TownNPCRespawnLockGlobalNPC.SuppressNextArrivalText = false;
				return; // teksnya ga jadi ditampilin
			}

			orig(text, r, g, b);
		}

		private delegate void orig_SpawnHomelessNPC();
		private static void SpawnHomelessNpcDetour(orig_SpawnHomelessNPC orig)
		{
			const int maxRetries = 5;

			for (int attempt = 0; attempt <= maxRetries; attempt++)
			{
				TownNPCRespawnLockGlobalNPC.ResetBlockedFlag();
				orig();

				if (!TownNPCRespawnLockGlobalNPC.LastSpawnAttemptBlocked)
					break; // spawn valid, atau emang ga ada yang coba spawn -> selesai
			}
		}

		public override void AddRecipeGroups()
		{
			// Definisi grup-nya sendiri ada di SoulCollectorAltarItem.cs,
			// di sini cuma manggil aja -- tModLoader ngewajibin hook ini
			// jalan dari class Mod, ga bisa dari ModItem.
			SoulCollectorAltarItem.RegisterRecipeGroups();
		}

		// =====================================================================
		// DISPATCHER PACKET CUSTOM (bukan MonoMod hook kayak yang di atas --
		// ini jalur ModPacket resmi tModLoader). Byte pertama di tiap packet
		// nentuin "jenis pesannya", sisanya diteruskan mentah-mentah (masih
		// dalam bentuk BinaryReader yang sama) ke SoulNetworking.HandlePacket()
		// biar semua logic baca/tulis field spesifik per-aksi (Convert/
		// Revive/Token slot) numpuk di 1 file (SoulNetworking.cs), bukan
		// nyebar di sini.
		//
		// whoAmI di sini adalah index Main.player[] punya CLIENT PENGIRIM --
		// tModLoader yang otomatis ngisi parameter ini pas manggil
		// HandlePacket, cuma valid/berguna di sisi SERVER (dedicated server
		// atau host). Client biasa ga akan pernah nerima ModPacket dari
		// client lain (semua ModPacket dari client selalu ke server dulu),
		// jadi method ini aman diasumsikan cuma jalan otoritatif.
		// =====================================================================
		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			byte messageType = reader.ReadByte();
			SoulNetworking.HandlePacket(messageType, reader, whoAmI);
		}
	}

	// =========================================================================
	// CONFIG: MENAMPILKAN ICON MOD DI FILTER BESTIARY (BESTIARY ICON LOCATION)
	// =========================================================================
	public class TheSanityBestiaryIcon : ModBiome
	{
		// PAKSA PATH: Mengarahkan game secara manual ke file "BestiaryIconMod.png" di folder utama
		public override string BestiaryIcon => "TheSanity/BestiaryIconMod";

		public override bool IsBiomeActive(Terraria.Player player) 
		{
			// Dikunci false karena ini murni hanya untuk memunculkan Icon di filter Bestiary
			return false; 
		}
	}
}