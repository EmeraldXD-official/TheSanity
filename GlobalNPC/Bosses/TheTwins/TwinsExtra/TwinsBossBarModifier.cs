using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework.Graphics;
using YourModName.Content.NPCs;

namespace TheSanity.GlobalNPCs
{
    // ==========================================
    // TwinsBossBarModifier — request "pas Spectre muncul, healthbar Twins original ada
    // kek health Shield-nya", persis pola yang udah dipakai EaterOfWorldsBossBarModifier
    // (lihat EaterOfWorldsHealthManager.cs) buat Jantung: bar utama tetap nunjukin HP Twin
    // ASLI apa adanya, terus ditumpuk overlay "Shield" yang isinya HP si ancaman sungguhan
    // saat itu (Jantung buat EoW, gabungan Spectre buat Twins).
    //
    // PENTING soal "total"-nya: SpectreRetinazer & SpectreSpazmatism BERBAGI SATU POOL
    // nyawa yang sama persis (lihat komentar "SHARED HEALTH POOL" di kedua file itu -
    // Retinazer nge-transfer damage-nya sendiri ke Spazmatism tiap tick, terus resync balik
    // ngikutin Spazmatism). Artinya NPC.life kedua Spectre SELALU SAMA PERSIS satu sama
    // lain di titik mana pun - kalau kita JUMLAHKAN keduanya (ret.life + spaz.life) hasilnya
    // malah DOBEL dari nilai pool yang sebenarnya. Jadi "total health Spectre" di sini
    // artinya AMBIL SATU nilai pool-nya (dari Spazmatism, si pemegang "sumber kebenaran"),
    // BUKAN menjumlahkan life dua instance NPC-nya.
    // ==========================================
    public class TwinsBossBarModifier : GlobalBossBar
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
        {
            if (npc.type != NPCID.Retinazer && npc.type != NPCID.Spazmatism)
                return true;

            // State "lagi ada Spectre" (PhaseTwoSpectresActive / LastStandHoldingForSpectres)
            // di-anchor ke instance GlobalNPC milik Spazmatism ASLI (bukan Spectre) - sama
            // persis pola yang dipakai TwinsReworkOverride buat baca flag ini dari NPC lain
            // (lihat trail after-image Retinazer di TwinsRework.cs).
            int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
            if (spazIndex == -1 || !Main.npc[spazIndex].active)
            {
                drawParams.Shield = 0f;
                drawParams.ShieldMax = 0f;
                return true;
            }

            TwinsReworkOverride spazGlobal = Main.npc[spazIndex].GetGlobalNPC<TwinsReworkOverride>();
            bool spectresActive = spazGlobal.PhaseTwoSpectresActive || spazGlobal.LastStandHoldingForSpectres;

            if (!spectresActive)
            {
                drawParams.Shield = 0f;
                drawParams.ShieldMax = 0f;
                return true;
            }

            // Cari SpectreSpazmatism (pemegang pool ASLI) buat baca total HP gabungannya -
            // gak perlu cari SpectreRetinazer sama sekali, nilainya udah pasti identik.
            NPC spectrePoolHolder = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && n.type == ModContent.NPCType<SpectreSpazmatism>())
                {
                    spectrePoolHolder = n;
                    break;
                }
            }

            if (spectrePoolHolder != null)
            {
                drawParams.Shield = spectrePoolHolder.life;
                drawParams.ShieldMax = spectrePoolHolder.lifeMax;
            }
            else
            {
                drawParams.Shield = 0f;
                drawParams.ShieldMax = 0f;
            }

            return true;
        }
    }
}
