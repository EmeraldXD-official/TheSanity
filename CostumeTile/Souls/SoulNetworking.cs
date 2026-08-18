using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // Custom packet buat 3 aksi altar (Convert, Revive, ambil/taro Soul
    // Token) biar BENERAN bisa dipencet dari CLIENT di multiplayer asli
    // (non-host) -- sebelumnya 3 aksi ini di-skip total di client (cuma
    // netMode guard, ga ngirim apa-apa sama sekali ke server), jadi
    // tombolnya "mati" kalau kamu bukan host/dedicated server.
    //
    // ALUR:
    //   Client klik tombol -> kirim request kecil (posisi altar + parameter
    //   seperlunya) lewat ModPacket -> server terima di
    //   TheSanity.HandlePacket() -> diteruskan ke HandlePacket() di sini ->
    //   divalidasi ULANG di sisi server (jangan percaya client mentah2) ->
    //   dieksekusi pake method OTORITATIF yang SAMA PERSIS kayak yang
    //   dipakai host/singleplayer (TryConvertSouls/BeginReviveRitual/dst) ->
    //   method2 itu udah manggil altar.Sync() sendiri di dalemnya, yang
    //   otomatis broadcast SoulCount/TokenSlotItem terbaru ke SEMUA client
    //   (termasuk yang ngirim request tadi) lewat TileEntitySharing yang
    //   udah ada -- jadi TIDAK perlu bikin sync tambahan lagi di sini.
    //
    // CATATAN: ModPacket dari client SELALU jalan ke server dulu (tModLoader
    // ga punya jalur client-ke-client langsung), jadi HandlePacket() di sini
    // aman diasumsikan CUMA jalan di sisi otoritatif (dedicated server / host).
    //
    // ANIMASI RITUAL REVIVE DI MULTIPLAYER: dulu animasi soul-terbang cuma
    // keliatan di host (SoulCollectorAltarEntity.Update() di-skip total di
    // client). Sekarang server broadcast RitualStartBroadcast ke SEMUA
    // client abis ritual beneran dimulai otoritatif -- tiap client lalu
    // manggil BeginReviveRitual() versi LOKALnya sendiri (lihat
    // HandleRitualStartBroadcast di bawah) buat mulai TICKING lokal yang
    // sama (Update() di SoulCollectorAltarEntity juga udah dipatch biar
    // ga skip total di client lagi -- yang tetep cuma jalan otoritatif
    // cuma SpawnRitualNPC()). Soul-soul & partikelnya MURNI kosmetik jadi
    // ga masalah kalau tiap layar keliatan sedikit beda (random spread
    // arahnya di-generate lokal per-client, ga dikirim lewat network).
    // =========================================================
    public static class SoulNetworking
    {
        public const byte ConvertRequest = 0;
        public const byte ReviveRequest = 1;
        public const byte TokenSlotTakeRequest = 2;
        public const byte TokenSlotDepositRequest = 3;

        // Server -> SEMUA client, dikirim abis ritual revive beneran dimulai
        // otoritatif. Lihat SendRitualStartBroadcast / HandleRitualStartBroadcast
        // di bawah buat penjelasan lengkap.
        public const byte RitualStartBroadcast = 4;

        // ---------------------------------------------------------------
        // SISI CLIENT -- bikin & kirim packet-nya
        // ---------------------------------------------------------------

        public static void SendConvertRequest(SoulCollectorAltarEntity altar)
        {
            ModPacket packet = ModContent.GetInstance<TheSanity>().GetPacket();
            packet.Write(ConvertRequest);
            packet.Write(altar.Position.X);
            packet.Write(altar.Position.Y);
            packet.Send();
        }

        public static void SendReviveRequest(SoulCollectorAltarEntity altar, int npcType)
        {
            ModPacket packet = ModContent.GetInstance<TheSanity>().GetPacket();
            packet.Write(ReviveRequest);
            packet.Write(altar.Position.X);
            packet.Write(altar.Position.Y);
            packet.Write(npcType);
            packet.Send();
        }

        public static void SendTokenSlotTakeRequest(SoulCollectorAltarEntity altar)
        {
            ModPacket packet = ModContent.GetInstance<TheSanity>().GetPacket();
            packet.Write(TokenSlotTakeRequest);
            packet.Write(altar.Position.X);
            packet.Write(altar.Position.Y);
            packet.Send();
        }

        // amount: banyaknya Soul Token yang mau dipindah dari cursor client
        // ke slot altar. Client udah ngurangin cursor-nya sendiri LOKAL
        // (optimis) SEBELUM manggil ini -- lihat SoulTokenSlotElement.LeftClick
        // di SoulCollectorUIState.cs. Ini konsisten sama model trust bawaan
        // Terraria sendiri (inventory/cursor player itu client-authoritative
        // buat barang miliknya sendiri).
        public static void SendTokenSlotDepositRequest(SoulCollectorAltarEntity altar, int amount)
        {
            ModPacket packet = ModContent.GetInstance<TheSanity>().GetPacket();
            packet.Write(TokenSlotDepositRequest);
            packet.Write(altar.Position.X);
            packet.Write(altar.Position.Y);
            packet.Write((short)amount);
            packet.Send();
        }

        // Dipanggil dari SERVER doang (dalem HandleReviveRequest, abis
        // altar.BeginReviveRitual() beneran kepanggil otoritatif). packet.Send()
        // TANPA parameter, dipanggil dari sisi server, artinya broadcast ke
        // SEMUA client yang lagi konek (termasuk client yang awalnya ngirim
        // ReviveRequest -- client itu SENDIRI belum jalanin ritual lokalnya
        // sama sekali, cuma ngirim request & nunggu, jadi dia juga WAJIB
        // kebagian broadcast ini).
        public static void SendRitualStartBroadcast(SoulCollectorAltarEntity altar, int npcType)
        {
            ModPacket packet = ModContent.GetInstance<TheSanity>().GetPacket();
            packet.Write(RitualStartBroadcast);
            packet.Write(altar.Position.X);
            packet.Write(altar.Position.Y);
            packet.Write(npcType);
            packet.Send();
        }

        // ---------------------------------------------------------------
        // SISI SERVER -- baca & eksekusi. Dipanggil dari TheSanity.HandlePacket().
        // ---------------------------------------------------------------

        public static void HandlePacket(byte messageType, BinaryReader reader, int whoAmI)
        {
            switch (messageType)
            {
                case ConvertRequest:
                    HandleConvertRequest(reader);
                    break;
                case ReviveRequest:
                    HandleReviveRequest(reader, whoAmI);
                    break;
                case TokenSlotTakeRequest:
                    HandleTokenSlotTakeRequest(reader, whoAmI);
                    break;
                case TokenSlotDepositRequest:
                    HandleTokenSlotDepositRequest(reader);
                    break;
                case RitualStartBroadcast:
                    HandleRitualStartBroadcast(reader);
                    break;
            }
        }

        private static bool TryGetAltar(BinaryReader reader, out SoulCollectorAltarEntity altar)
        {
            short x = reader.ReadInt16();
            short y = reader.ReadInt16();

            altar = null;
            if (TileEntity.ByPosition.TryGetValue(new Point16(x, y), out TileEntity te)
                && te is SoulCollectorAltarEntity found)
            {
                altar = found;
                return true;
            }
            return false;
        }

        private static void HandleConvertRequest(BinaryReader reader)
        {
            if (!TryGetAltar(reader, out SoulCollectorAltarEntity altar))
                return;

            // TryConvertSouls() ngecek ulang CanConvert di dalemnya sendiri --
            // request yang ga valid (soul kurang / slot token udah full stack
            // item lain) otomatis di-skip diam-diam di sana, ga perlu dobel
            // validasi di sini.
            altar.TryConvertSouls();
        }

        private static void HandleReviveRequest(BinaryReader reader, int whoAmI)
        {
            if (!TryGetAltar(reader, out SoulCollectorAltarEntity altar))
                return;

            int npcType = reader.ReadInt32();

            // Validasi ulang di sisi server -- SAMA PERSIS sama syarat yang
            // dicek di SoulCollectorUIState.TryRevive() buat jalur host/
            // singleplayer, biar client ga bisa "maksa" revive NPC yang ga
            // valid cuma dengan ngirim packet manual.
            if (SoulCollectorAltarEntity.AnyRitualActive)
                return;
            if (!SoulTrackerSystem.DiscoveredNPCs.Contains(npcType))
                return;
            if (SoulTrackerSystem.IsAlive(npcType))
                return;
            if (SoulTrackerSystem.GetKillCount(npcType) <= 0)
                return;
            if (altar.SoulCount < SoulCollectorAltarEntity.ReviveCost)
                return;

            Player player = Main.player[whoAmI];

            altar.RemoveSoul(SoulCollectorAltarEntity.ReviveCost);
            altar.BeginReviveRitual(npcType, player);

            // Kabarin SEMUA client (lihat catatan panjang di atas class) biar
            // masing2 mulai jalanin animasi ritual LOKALnya sendiri juga --
            // tanpa ini cuma sisi server yang keliatan animasinya.
            SendRitualStartBroadcast(altar, npcType);
        }

        // ---------------------------------------------------------------
        // SISI CLIENT -- nerima broadcast ritual mulai, jalanin copy lokal
        // animasinya sendiri lewat method OTORITATIF yang sama
        // (BeginReviveRitual) -- method itu sendiri AMAN dipanggil di
        // client soalnya cuma nyiapin state animasi (IsRitualActive +
        // daftar soul), TIDAK ngurangin SoulCount ataupun nyiptain NPC.
        // ---------------------------------------------------------------
        private static void HandleRitualStartBroadcast(BinaryReader reader)
        {
            if (!TryGetAltar(reader, out SoulCollectorAltarEntity altar))
                return;

            int npcType = reader.ReadInt32();
            altar.BeginReviveRitual(npcType, Main.LocalPlayer);
        }

        private static void HandleTokenSlotTakeRequest(BinaryReader reader, int whoAmI)
        {
            if (!TryGetAltar(reader, out SoulCollectorAltarEntity altar))
                return;

            if (altar.TokenSlotItem.IsAir)
                return;

            Player player = Main.player[whoAmI];
            Item taken = altar.TokenSlotItem.Clone();

            altar.TokenSlotItem = new Item();
            altar.Sync();

            // CATATAN API: Player.QuickSpawnItem() adalah helper tModLoader
            // (bukan vanilla) yang nge-spawn Item di dunia dan langsung nyoba
            // masukin ke INVENTORY player tujuan (net-sync otomatis lewat
            // jalur item pickup normal) -- beda kecil dari singleplayer yang
            // taro item persis di mouse cursor, tapi ini cara paling aman/
            // simpel buat "ngasih" item ke player tertentu dari sisi server.
            // Kalau nama/signature method ini beda di versi tModLoader kamu
            // (compile error), ganti jadi drop manual di dunia:
            //   Item.NewItem(new EntitySource_TileUpdate(altar.Position.X, altar.Position.Y),
            //       player.position, player.width, player.height, taken.type, taken.stack);
            player.QuickSpawnItem(
                new EntitySource_TileUpdate(altar.Position.X, altar.Position.Y),
                taken, taken.stack);
        }

        private static void HandleTokenSlotDepositRequest(BinaryReader reader)
        {
            if (!TryGetAltar(reader, out SoulCollectorAltarEntity altar))
                return;

            short amount = reader.ReadInt16();
            if (amount <= 0)
                return;

            int tokenType = ModContent.ItemType<SoulToken>();
            Item stored = altar.TokenSlotItem;

            if (stored.IsAir)
            {
                stored = new Item();
                stored.SetDefaults(tokenType);
                stored.stack = 0;
                altar.TokenSlotItem = stored;
            }
            else if (stored.type != tokenType)
            {
                return; // slot kepake item lain -- harusnya ga mungkin, jaga-jaga aja
            }

            int space = stored.maxStack - stored.stack;
            int move = space < (int)amount ? space : (int)amount;
            if (move <= 0)
                return;

            stored.stack += move;
            altar.Sync();
        }
    }
}
