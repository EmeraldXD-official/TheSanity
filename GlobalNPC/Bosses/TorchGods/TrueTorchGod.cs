using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TorchGods.Patterns;

namespace TheSanity.GlobalNPC.Bosses.TorchGods
{
	/// <summary>
	/// Rework TorchGod jadi boss/miniboss NPC beneran.
	///
	/// === PENTING: AI() CUSTOM, BUKAN VANILLA ===
	/// Sebelumnya boss ini pakai AIType = NPCID.TorchGod (full vanilla AI).
	/// Itu DIBUANG karena AI vanilla Torch God itu bukan didesain buat boss
	/// yang bisa digebukin normal - itu AI buat event "dodge the projectiles",
	/// yang secara internal sering nyetel NPC.alpha tinggi (jadi keliatan
	/// transparan) dan mindahin posisi/target NPC ke tempat yang gak selalu
	/// deket player. Sekarang AI()-nya full custom (method di bawah): NPC
	/// DIEM di titik tengah arena (cuma idle-bob dikit, gak ngejar player),
	/// alpha dipaksa 0 (solid) tiap tick, dan hit-detection normal kayak
	/// boss lain (bisa kena melee/ranged/mage/summon dari jarak wajar).
	///
	/// === MODE SPRITE SAAT INI: PLACEHOLDER VANILLA ===
	/// Karena sprite custom (TrueTorchGod.png) BELUM ADA, sementara boss ini
	/// pakai sprite + animasi VANILLA Torch God apa adanya (lewat AnimationType,
	/// dan Texture di-override manual ke tekstur vanilla). Begitu sprite custom
	/// 71-frame kamu udah siap, kasih tau aku lagi - kita pasang balik texture
	/// override ke path custom + PreDraw manual dengan crossfade (kode itu
	/// sempat kubuat sebelumnya, tinggal diaktifin lagi).
	///
	/// Logic spawn (posisi awal ikut player, lempar player ke arah acak,
	/// munculin border arena) SENGAJA TIDAK ada di sini - itu semua ada di
	/// TorchGodArenaGlobalNPC.cs.
	///
	/// Cara spawn ITEM belum ada dulu (nanti disiapkan terpisah) - untuk
	/// sekarang NPC ini baru bisa dites lewat command /spawnnpc atau semacamnya.
	/// </summary>
	[AutoloadBossHead]
	public class TrueTorchGod : ModNPC
	{
		// SizeMultiplier sekarang CUMA ngaruh ke ukuran VISUAL (NPC.scale) -
		// hitbox-nya udah dipisah, fixed 3x2 block (lihat SetDefaults).
		private const float SizeMultiplier = 2.5f;

		// === HP per difficulty ===
		// Target: 10.000 HP di MASTER MODE. Master = 3x dari Normal/Classic,
		// jadi baseline Normal-nya sengaja diset 10000/3 (dibulatkan), dan
		// Expert di antara keduanya (2x dari Normal, pola umum vanilla).
		private const int LifeNormal = 3334;
		private const int LifeExpert = 6667;
		private const int LifeMaster = 10000;

		// Defense FLAT di semua difficulty (gak ikut dinaikin otomatis).
		private const int DefenseAllDifficulty = 20;

		// === Pattern-pattern boss ini ===
		// Semua logic attack/intro ada di file terpisah (folder Patterns/) -
		// class ini cuma NYIMPEN instance-nya dan MANGGIL Update() tiap tick.
		// Nambah pattern baru nanti = bikin class baru di Patterns/, terus
		// tambahin instance-nya di sini + panggil di PostAI().
		private readonly TorchGodLifeRevealPattern lifeReveal = new();
		private readonly TorchGodSpiralFireballPattern spiralFireball = new();
		private readonly TorchGodLaserSweepPattern laserSweep = new();

		// Sementara pakai sprite vanilla (lihat catatan MODE SPRITE di atas).
		public override string Texture => "Terraria/Images/NPC_" + NPCID.TorchGod;

		public override void SetStaticDefaults()
		{
			NPCID.Sets.BossBestiaryPriority.Add(NPC.type);

			// WAJIB: AnimationType (di SetDefaults) cuma nentuin POLA animasi yang
			// dipakai, TAPI jumlah frame-nya (berapa banyak "potongan" gambar di
			// spritesheet) TIDAK ikut ke-copy otomatis. Kalau baris ini kelewat,
			// game salah nebak tinggi tiap frame pas nge-crop texture vanilla
			// Torch God -> hasilnya sprite kelihatan patah-patah / gambar acak
			// antar frame. Harus disamain persis sama frame count vanilla-nya.
			Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.TorchGod];
		}

		public override void SetDefaults()
		{
			// Ambil default vanilla Torch God dulu (buat stat lain yang gak
			// kita override manual, misal value jual, dll).
			NPC.CloneDefaults(NPCID.TorchGod);

			// SENGAJA TIDAK set AIType lagi (lihat catatan di atas class) -
			// gerak/posisi/attack full ditentuin AI() custom di bawah.

			// Animasi tetap ikut vanilla (lihat catatan MODE SPRITE) - ini
			// cuma nentuin CARA frame di-cycle, gak nyangkut ke AIType.
			AnimationType = NPCID.TorchGod;

			// Hitbox dipaksa FIXED 3 block x 2 block (1 block = 16px), BUKAN
			// ikut hasil kali SizeMultiplier dari hitbox vanilla (yang kecil
			// banget, makanya dulu susah kena). Sengaja gede biar enak
			// di-hit - gak perlu presisi banget buat nyerempet boss ini.
			const int HitboxBlockWidth = 3;
			const int HitboxBlockHeight = 2;
			const int PixelsPerBlock = 16;

			NPC.width = HitboxBlockWidth * PixelsPerBlock;   // 48
			NPC.height = HitboxBlockHeight * PixelsPerBlock; // 32

			// Sprite masih placeholder vanilla (kecil) - scale visual biar
			// gak keliatan aneh dibanding hitbox barunya. Ini CUMA visual,
			// gak ngaruh ke ukuran hitbox di atas.
			NPC.scale = SizeMultiplier;

			NPC.boss = true;
			NPC.defense = DefenseAllDifficulty;
			NPC.knockBackResist = 0f; // Imun total ke knockback (0f = 0% kena dorongan)

			// Baseline Normal/Classic - override final per-difficulty ada di
			// ScaleExpertStats di bawah (dipanggil otomatis pas world Expert/Master).
			NPC.lifeMax = LifeNormal;

			// NPC.boss = true otomatis bikin health bar boss standar muncul,
			// gak perlu kode tambahan buat itu.
		}

		// Dipanggil tModLoader otomatis kalau world Expert/Master, buat nentuin
		// stat final boss ini di difficulty tsb. Override manual di sini biar
		// angkanya PASTI (10k di Master), bukan ngikut formula default.
		// (Nama hook ini di versi tModLoader baru: ApplyDifficultyAndPlayerScaling,
		// dulu namanya ScaleExpertStats - kalau tModLoader-mu versi lama dan
		// masih error, ganti nama method ini balik ke ScaleExpertStats.)
		public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
		{
			NPC.lifeMax = Main.masterMode ? LifeMaster : LifeExpert;
			NPC.defense = DefenseAllDifficulty; // tetap flat, gak ikut naik
		}

		// === Konstanta "diem di tengah arena" ===
		private const float BobAmplitude = 6f;    // seberapa jauh naik-turun idle bob-nya
		private const float BobSpeed = 0.05f;     // makin gede makin cepet naik-turunnya

		// Titik "tengah arena" - di-capture SEKALI di tick AI() pertama. Ini
		// aman karena TorchGodArenaGlobalNPC.OnSpawn udah maksa npc.Center
		// pas di titik center arena SEBELUM tick AI() pertama jalan.
		private Vector2 anchorPosition;
		private bool anchorCaptured = false;

		/// <summary>
		/// AI custom penuh (gantiin AIType vanilla). Boss diem di titik
		/// center arena (cuma idle-bob naik turun dikit biar gak keliatan
		/// kayak gambar statis), gak ngejar-ngejar player. Attack/pattern
		/// tetap di PostAI (biar konsisten sama struktur pattern yang udah ada).
		/// </summary>
		public override void AI()
		{
			// Paksa solid tiap tick - benerin bug "cuma afterimage yang
			// keliatan, badan asli transparan" (AI vanilla lama suka nyetel
			// alpha tinggi buat NPC ini, sekarang kita override balik).
			NPC.alpha = 0;

			// Redundant safety: paksa juga di sini (bukan cuma di PostAI),
			// biar dijamin false SEBELUM game ngecek collision damage tick
			// ini juga - kalau sebelumnya kerasa "gak bisa kena sama sekali",
			// kemungkinan besar itu karena nge-tes pas masih fase life-reveal
			// (dontTakeDamage emang SENGAJA true di situ, ~2.5 detik pertama).
			if (lifeReveal.IsDone)
				NPC.dontTakeDamage = false;

			if (!anchorCaptured)
			{
				anchorPosition = NPC.Center;
				anchorCaptured = true;

				// Pastiin dari awal dia gak kepengaruh gravitasi/fisika lain
				// sama sekali - CloneDefaults(NPCID.TorchGod) SEHARUSNYA udah
				// bawa noGravity = true (vanilla Torch God emang floating),
				// tapi kita paksa ulang di sini biar dijamin, apapun yang
				// terjadi.
				NPC.noGravity = true;
				NPC.noTileCollide = true;
			}

			// Idle bob naik-turun di sekitar anchor - posisi horizontal TETAP,
			// cuma geser dikit secara vertikal biar kerasa "melayang", bukan
			// diem kaku kayak patung.
			float bobOffset = MathF.Sin(Main.GameUpdateCount * BobSpeed) * BobAmplitude;

			// LANGSUNG TIMPA posisi (bukan cuma nyetel velocity terus
			// nunggu di-integrate) - ini yang bikin dia BENERAN diem, gak
			// "ketarik-tarik" pelan doang. Velocity dipaksa 0 juga biar gak
			// ada sisa momentum dari knockback/fisika lain yang numpuk dan
			// bikin dia keseret sebelum sempat kekoreksi balik. Efek
			// sampingnya: proyektil non-homing sekarang seharusnya kena
			// normal juga, karena posisinya udah gak geser-geser lagi pas
			// proyektil-nya nyampe.
			NPC.Center = anchorPosition + new Vector2(0f, bobOffset);
			NPC.velocity = Vector2.Zero;

			// Hadap ke arah player terdekat - kosmetik doang, gak mempengaruhi
			// posisi (boss tetap diem di tengah).
			Player closest = Main.player[Player.FindClosest(NPC.Center, NPC.width, NPC.height)];
			if (closest != null && closest.active)
				NPC.spriteDirection = closest.Center.X > NPC.Center.X ? 1 : -1;
		}

		// ModNPC gak punya hook OnSpawn (itu punya GlobalNPC) - jadi kita init
		// pattern-nya lazy di tick PERTAMA PostAI aja, pakai flag ini.
		private bool initialized = false;

		// Jalan tiap tick SETELAH AI() (custom) di atas selesai. Class ini
		// cuma orkestrasi urutan pattern - logic aslinya ada di masing-masing
		// class pattern (folder Patterns/).
		public override void PostAI()
		{
			if (!initialized)
			{
				lifeReveal.Reset(NPC);
				initialized = true;
			}

			// Begitu life-reveal BARU AJA kelar (return true di tick itu doang),
			// langsung trigger pattern berikutnya: spiral fireball.
			if (lifeReveal.Update(NPC))
				spiralFireball.Activate();

			// Begitu spiral fireball BARU AJA kelar (1 muteran penuh abis),
			// lanjut ke laser sweep.
			if (spiralFireball.Update(NPC))
				laserSweep.Activate(NPC);

			// Begitu laser sweep BARU AJA kelar (telegraph + muter abis),
			// balik lagi ke spiral fireball - jadinya kedua attack ini
			// LOOPING bergantian terus selama fight berlangsung.
			if (laserSweep.Update())
				spiralFireball.Activate();

			// Jaga-jaga: pastiin dontTakeDamage tetap false tiap tick abis
			// reveal kelar (harusnya udah gak ada yang toggle balik lagi
			// sekarang karena AI() udah full custom, bukan vanilla - tapi
			// ini murah buat dipertahanin sebagai safety net).
			if (lifeReveal.IsDone)
				NPC.dontTakeDamage = false;
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			Texture2D tex = TextureAssets.Npc[NPC.type].Value;
			Rectangle sourceRect = NPC.frame; // udah otomatis diurus AnimationType
			Vector2 origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
			Vector2 drawPos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);
			SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

			// Badan utama (sprite asli, gak ditransformasi) SENGAJA GAK
			// digambar - yang keliatan cuma 2 afterimage yang orbit di bawah
			// ini. (Sebelumnya ada juga "glow copies" nempel di posisi
			// tengah - itu udah dihapus juga karena keliatan kayak badan
			// utama juga, cuma buram.)

			// === 2 afterimage oranye-kuning, kecil, muter di sekeliling titik tengah NPC ===
			const float OrbitRadius = 20f;
			const float AfterimageOrbitSpeed = 0.05f; // radian per tick
			const float AfterimageScaleMultiplier = 0.45f; // lebih kecil dari badan asli

			float baseAngle = Main.GameUpdateCount * AfterimageOrbitSpeed;
			Color afterimageColor = new Color(255, 200, 70) * 0.7f;

			for (int i = 0; i < 2; i++)
			{
				// Dua afterimage saling berlawanan (180 derajat), biar keliatan
				// muter ngelilingin titik tengahnya dari 2 sisi sekaligus.
				float orbitAngle = baseAngle + MathHelper.Pi * i;
				Vector2 orbitOffset = orbitAngle.ToRotationVector2() * OrbitRadius;

				spriteBatch.Draw(tex, drawPos + orbitOffset, sourceRect, afterimageColor, NPC.rotation, origin,
					NPC.scale * AfterimageScaleMultiplier, effects, 0f);
			}

			// return false -> sprite ASLI (badan utama) TIDAK digambar sama
			// sekali. Kalau nanti mau balikin badan utamanya lagi, ganti ini
			// jadi return true.
			return false;
		}
	}
}

