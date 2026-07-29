using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace TheSanity
{
    // CLASS NPC Tetap Dibuat untuk Keperluan Pendaftaran / Filter Sesuai Struktur Folder
    public class SkeletonSniperRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.SkeletonSniper;
        }
    }

    // =========================================================================
    // [PROJECTILE REWORK SYSTEM]: FORCE REPLACE TYPE SAAT SPAWN
    // =========================================================================
    public class SniperVenomBulletModifier : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // Hook OnSpawn untuk mencegat peluru tepat saat keluar dari moncong senjata
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Cek apakah proyektil ini ditembakkan oleh sebuah NPC
            if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
            {
                // Pastikan NPC penembaknya adalah Skeleton Sniper dan pelurunya adalah SniperBullet
                if (npc.type == NPCID.SkeletonSniper && projectile.type == ProjectileID.SniperBullet)
                {
                    // --- FORCE REPLACE VARIABEL DASAR ---
                    projectile.type = ProjectileID.VenomBullet;
                    projectile.aiStyle = ProtoTypeIDForAIStyle(ProjectileID.VenomBullet); // Set gaya gerak peluru venom
                    
                    // Paksa status agar murni memusuhi player
                    projectile.hostile = true;
                    projectile.friendly = false;

                    // Reset ulang ukuran fisik proyektil agar sesuai dengan jenis Venom Bullet
                    projectile.width = 4;
                    projectile.height = 4;

                    projectile.netUpdate = true;
                }
            }
        }

        // Fungsi pembantu internal untuk menyalin aiStyle peluru target secara aman
        private int ProtoTypeIDForAIStyle(int projectileID)
        {
            // Mengembalikan aiStyle peluru bullet konvensional (biasanya aiStyle = 1)
            return 1; 
        }

        // =========================================================================
        // [DEBUFF INFLICT LOCATION]: MEMBERIKAN EFEK VENOM PADA PLAYER
        // =========================================================================
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Pastikan efek hanya bekerja jika peluru venom tersebut berstatus milik musuh (hasil force ganti sniper)
            if (projectile.type == ProjectileID.VenomBullet && projectile.hostile)
            {
                // -------------------------------------------------------------------------
                // [DEBUFF BALANCING LOCATION]: ID 94 (Acid Venom) selama 5 Detik (300 Ticks)
                // -------------------------------------------------------------------------
                int venomDuration = 300; 
                target.AddBuff(BuffID.Venom, venomDuration);
            }
        }
    }
}