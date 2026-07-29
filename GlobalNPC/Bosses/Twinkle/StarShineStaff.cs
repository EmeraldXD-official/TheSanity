using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class StarShineStaff : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/StarShineStaff";

        public override void SetStaticDefaults() {
            Item.staff[Type] = true; 
        }

        public override void SetDefaults() {
            Item.damage = 25;
            Item.DamageType = DamageClass.Magic;
            Item.mana = 5;
            
            Item.width = 40;
            Item.height = 40;
            
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true; 
            Item.knockBack = 3.5f;
            
            Item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>(); 
            Item.value = Item.buyPrice(gold: 1, silver: 50);
            Item.UseSound = SoundID.Item9;
            
            Item.shoot = ModContent.ProjectileType<StarShineProj>();
            Item.shootSpeed = 12f; 
        }

        // 1. MEMBATASI VISUAL TONGKAT (Agar tangan player mentok di pojok)
        public override void UseStyle(Player player, Rectangle heldItemFrame) {
            // Hitung arah mentah dari player ke posisi mouse cursor
            Vector2 targetDir = (Main.MouseWorld - player.MountedCenter).SafeNormalize(Vector2.UnitX * player.direction);
            
            // Batasi sudutnya menggunakan fungsi pembantu di bawah
            ClampSudutSihir(player, ref targetDir);

            // Pasang hasil sudut yang sudah dikunci ke rotasi badan player
            float rotasiFinal = targetDir.ToRotation();
            
            // Koreksi algoritma internal Terraria khusus senjata jenis Gem Staff
            if (rotasiFinal < -1.57f) rotasiFinal += 3.14f;
            else if (rotasiFinal > 1.57f) rotasiFinal -= 3.14f;
            
            player.itemRotation = rotasiFinal;
        }

        // 2. MEMBATASI PELURU & POSISI TEMBAK (Agar peluru keluar selaras dengan visual)
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // Ambil arah peluru saat ini
            Vector2 targetDir = velocity.SafeNormalize(Vector2.Zero);
            
            // Batasi sudut peluru agar sama persis dengan visual tongkatnya
            ClampSudutSihir(player, ref targetDir);
            
            // Kembalikan kecepatan peluru (12f) dengan arah baru yang sudah dikunci
            velocity = targetDir * Item.shootSpeed;

            // Setingan tinggi dan jarak 6 blok kedepan yang kemarin
            position.Y -= 32f;
            Vector2 muzzleOffset = targetDir * 96f;
            if (Collision.CanHit(position, 0, 0, position + muzzleOffset, 0, 0)) {
                position += muzzleOffset;
            }
        }

        // 🛠️ FUNGSI UTAMA UNTUK MENGUNCI SUDUT (Mekanik Clamping)
        private void ClampSudutSihir(Player player, ref Vector2 dir) {
            // KUNCI SETINGAN: 0.707f melambangkan sudut 45 derajat (Pas pojok diagonal)
            // - Semakin besar angkanya (misal 0.85f), tembakan akan semakin ceper/horizontal.
            // - Semakin kecil angkanya (misal 0.30f), tembakan baru bisa mendekati vertikal.
            float limitX = 0.707f; 

            if (player.direction == 1) { // Jika player menghadap KANAN
                if (dir.X < limitX) {
                    dir.X = limitX;
                    dir.Y = limitX * (dir.Y < 0 ? -1f : 1f); // Paksa mentok ke pojok atas atau pojok bawah
                }
            } 
            else { // Jika player menghadap KIRI
                if (dir.X > -limitX) {
                    dir.X = -limitX;
                    dir.Y = limitX * (dir.Y < 0 ? -1f : 1f); // Paksa mentok ke pojok atas atau pojok bawah
                }
            }
            dir = Vector2.Normalize(dir); // Segarkan kembali kalkulasi vektornya
        }
    }
}