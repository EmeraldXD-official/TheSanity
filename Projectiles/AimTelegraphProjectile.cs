using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class AimTelegraphProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0"; 

        public int CasterIndex {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }

        // Membaca jenis kustom indikator: 1 = Hantaman Jatuh (Bawah), 0 = Sniper (Incar Player)
        public float TelegraphMode => Projectile.ai[1];

        public override void SetDefaults()
        {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.hostile = false; 
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            
            // Mengikuti durasi hantaman setup di AI (60 tick)
            Projectile.timeLeft = 60; 
        }

        public override void AI()
        {
            NPC boss = Main.npc[CasterIndex];
            if (!boss.active)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = boss.Center;

            // CEK MODE SERANGAN
            if (TelegraphMode == 1f)
            {
                // JURUS HANTAMAN: Benar-benar tegak lurus mengarah ke tanah bawah King Slime!
                Projectile.rotation = MathHelper.PiOver2; 
            }
            else
            {
                // JURUS SNIPER: Mengunci koordinat/arah lurus ke pemain
                Player target = Main.player[boss.target];
                if (target != null && target.active && !target.dead)
                {
                    Vector2 arahKeTarget = target.Center - Projectile.Center;
                    Projectile.rotation = arahKeTarget.ToRotation();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            NPC boss = Main.npc[CasterIndex];
            Player target = Main.player[boss.target];

            if (target == null || !target.active) return false;

            Vector2 posisiMulai = Projectile.Center;
            float rotasiPanah = Projectile.rotation;

            // =========================================================================
            // 🛠️ PANDUAN KUSTOMISASI 6: JARAK JANGKAUAN DAN SIZING BEAM TELEGRAPH
            // =========================================================================
            // Jika mode hentakan, panjangkan balok indikator ke bawah sejauh 1600 pixel. Jika sniper, sejauh jarak player.
            float totalJarak = (TelegraphMode == 1f) ? 1600f : (target.Center - posisiMulai).Length();
            
            // DIUBAH: Ketebalan balok sekarang dinamis mengikuti total lebar badan asli King Slime (boss.width)
            float ketebalanBadan = boss.width; 

            float faktorAnimasi = (MathF.Sin((float)Main.time * 0.12f) + 1f) / 2f; 
            Color warnaUtama = Color.Lerp(new Color(0, 180, 255), new Color(0, 50, 220), faktorAnimasi);
            Color warnaBackground = warnaUtama * 0.25f; // Transparansi latar balok kotak

            Vector2 originSisi = new Vector2(0f, 0.5f);
            Vector2 posisiLayarMulai = posisiMulai - Main.screenPosition;

            // 1. GAMBAR KOTAK BACKGROUND INDIKATOR (SEUKURAN BADAN BOSS)
            Main.EntitySpriteDraw(
                pixel, 
                posisiLayarMulai, 
                new Rectangle(0, 0, 1, 1), 
                warnaBackground, 
                rotasiPanah, 
                originSisi, 
                new Vector2(totalJarak, ketebalanBadan), 
                SpriteEffects.None, 
                0
            );

            // 2. GAMBAR DERETAN PANAH INDIKATOR INTERNAL
            float jarakAntarV = 70f;      
            float panjangSayapV = 35f;     
            float tebalGarisV = 6f;       
            float sudutKemiringanV = MathHelper.ToRadians(135f); 

            for (float d = jarakAntarV; d < totalJarak; d += jarakAntarV)
            {
                Vector2 posisiV = posisiMulai + Vector2.UnitX.RotatedBy(rotasiPanah) * d;
                Vector2 posisiVDiLayar = posisiV - Main.screenPosition;

                Main.EntitySpriteDraw(pixel, posisiVDiLayar, new Rectangle(0, 0, 1, 1), warnaUtama, rotasiPanah + sudutKemiringanV, originSisi, new Vector2(panjangSayapV, tebalGarisV), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(pixel, posisiVDiLayar, new Rectangle(0, 0, 1, 1), warnaUtama, rotasiPanah - sudutKemiringanV, originSisi, new Vector2(panjangSayapV, tebalGarisV), SpriteEffects.None, 0);
            }

            // 3. GAMBAR UJUNG PANAH BESAR DI AKHIR LINE
            float panjangKepalaV = 70f;   
            float tebalKepalaV = 10f;      
            float sudutLebarV = MathHelper.ToRadians(145f); 

            // Tentukan di mana kepala panah digambar
            Vector2 ujungKoordinat = (TelegraphMode == 1f) ? posisiMulai + new Vector2(0f, totalJarak) : target.Center;
            Vector2 posisiLayarTarget = ujungKoordinat - Main.screenPosition;

            Main.EntitySpriteDraw(pixel, posisiLayarTarget, new Rectangle(0, 0, 1, 1), warnaUtama, rotasiPanah + sudutLebarV, originSisi, new Vector2(panjangKepalaV, tebalKepalaV), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, posisiLayarTarget, new Rectangle(0, 0, 1, 1), warnaUtama, rotasiPanah - sudutLebarV, originSisi, new Vector2(panjangKepalaV, tebalKepalaV), SpriteEffects.None, 0);

            return false; 
        }
    }
}