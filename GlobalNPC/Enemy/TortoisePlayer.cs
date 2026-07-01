using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class TortoisePlayer : ModPlayer
    {
        public int forcedLaunchTimer = 0;
        public Vector2 forcedLaunchVel = Vector2.Zero;

        // FIX HOOK ERROR: Menggunakan PreUpdateMovement agar posisi koordinat 
        // langsung digeser paksa sebelum game memproses pergerakan normal.
        public override void PreUpdateMovement()
        {
            if (forcedLaunchTimer > 0)
            {
                forcedLaunchTimer--;

                // Prediksi posisi frame berikutnya
                Vector2 nextPosition = Player.position + forcedLaunchVel;

                // Cek Tabrakan Dinding (Smart Collision Bouncing)
                if (!Collision.CanHitLine(Player.position, Player.width, Player.height, nextPosition, Player.width, Player.height))
                {
                    // Jika mentok kanan/kiri, balikkan arah horizontal ke depan
                    if (!Collision.CanHitLine(Player.position, Player.width, Player.height, new Vector2(nextPosition.X, Player.position.Y), Player.width, Player.height))
                    {
                        forcedLaunchVel.X *= -0.98f; 
                    }
                    // Jika mentok atas/bawah, balikkan arah vertikal
                    if (!Collision.CanHitLine(Player.position, Player.width, Player.height, new Vector2(Player.position.X, nextPosition.Y), Player.width, Player.height))
                    {
                        forcedLaunchVel.Y *= -0.98f;
                    }
                }

                // FORCE POSITION: Geser paksa koordinat player melewati rintangan
                Player.position += forcedLaunchVel;

                // Berikan efek perlambatan alami (Friction)
                forcedLaunchVel *= 0.95f;

                // Matikan kontrol pergerakan player selama terlempar
                Player.controlLeft = false;
                Player.controlRight = false;
                Player.controlUp = false;
                Player.controlDown = false;

                // Buat partikel debu tanah di sepanjang jalur pentalan paksa
                if (Main.rand.NextBool(2))
                {
                    Dust.NewDust(Player.position, Player.width, Player.height, 150, 0f, 0f, 100, default, 1.2f);
                }
            }
        }
    }
}