using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ID;
using Terraria.ModLoader;
using YourModName.Content.NPCs;

namespace TheSanity.BossBars
{
    // ==========================================
    // TheTwinsBars — custom ModBossBar buat The Twins (Retinazer & Spazmatism), format
    // sprite 11x12 numpang pola yang sama kayak Fargo's Soul Mod (lihat sample
    // AbominationnBossBar yang dikasih sebagai referensi struktur file). Texture-nya:
    //   E:\My Games\Terraria\tModLoader\ModSources\TheSanity\BossBars\TheTwinsBars.png
    // Dimensi/frame layout PNG WAJIB ngikutin format 11x12 vanilla (sama kayak punya
    // Fargo) biar BossBarLoader bisa gambar otomatis tanpa perlu kode draw manual
    // tambahan sama sekali - kita cuma perlu nunjuk path-nya lewat Texture di bawah.
    //
    // GETAR SELAMA LAST STAND — request "pas Last Stand, Health bar-nya bergetar-getar
    // sampai si Twin-nya mati". LastStandActive true dari TwinsLastStand.Trigger() (awal
    // event) SAMPAI PERSIS SETELAH npc.checkDead() dipanggil manual di
    // TwinsLastStand.TickDeathAnimation (baris terakhir sebelum LastStandActive di-set
    // false lagi) - lihat TwinsLastStand.cs. Artinya window ini nyakup SELURUH Last Stand:
    // Roar+refill, Deathray muter, sampai death animation explosion 5 detik - PERSIS
    // "sampai beneran mati" yang diminta, bukan cuma pas HP kritis doang kayak shake bawaan
    // Fargo punya (life == 1).
    //
    // State-nya (LastStandActive) itu field per-instance (InstancePerEntity = true di
    // TwinsReworkOverride) yang CUMA di-Set/dibaca dari sisi Spazmatism (lihat komentar di
    // TwinsRework.cs CheckActive/CheckDead) - jadi WAJIB nengok ke instance GlobalNPC milik
    // Spazmatism buat tau status-nya yang sebenarnya, gak peduli npc yang lagi digambar
    // bar-nya itu Retinazer atau Spazmatism.
    // ==========================================
    public class TheTwinsBars : ModBossBar
    {
        // Path eksplisit (gak ngandelin auto-detect ModTexturedType) biar gak ambigu -
        // WAJIB persis sama kayak lokasi file PNG kamu (folder BossBars, nama file sama
        // persis kayak nama class ini).
        public override string Texture => "TheSanity/BossBars/TheTwinsBars";

        // Seberapa jauh (px) bar-nya boleh "mencelat" tiap tick pas lagi getar. Silakan
        // di-tune - makin gede makin heboh getarannya.
        private const float ShakeStrength = 1.5f;

        private int bossHeadIndex = -1;

        public override Asset<Texture2D> GetIconTexture(ref Rectangle? iconFrame)
        {
            if (bossHeadIndex != -1)
            {
                return TextureAssets.NpcHeadBoss[bossHeadIndex];
            }
            return null;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
        {
            if (IsLastStandActive())
            {
                drawParams.BarCenter += Main.rand.NextVector2Circular(ShakeStrength, ShakeStrength);
            }

            return true;
        }

        private static bool IsLastStandActive()
        {
            int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
            if (spazIndex == -1 || !Main.npc[spazIndex].active)
                return false;

            return Main.npc[spazIndex].GetGlobalNPC<TwinsReworkOverride>().LastStandActive;
        }

        public override bool? ModifyInfo(ref BigProgressBarInfo info, ref float life, ref float lifeMax, ref float shield, ref float shieldMax)
        {
            NPC npc = Main.npc[info.npcIndexToAimAt];
            if (npc.townNPC || !npc.active)
                return false;

            life = npc.life;
            lifeMax = npc.lifeMax;

            bossHeadIndex = npc.GetBossHeadTextureIndex();
            return true;
        }
    }
}

