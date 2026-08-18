using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Luminance.Assets;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // TwinsDeathExplosionFX — ganti "ledakan" death animation Last Stand dari proyektil
    // vanilla DD2ExplosiveTrapT1/T2/T3Explosion (VISUAL DOANG, damage di-nol-in manual di
    // TwinsLastStand lama) ke particle custom pakai asset bloom ASLI Luminance
    // (MiscTexturesRegistry.BloomCircleSmall / BloomFlare) - biar "bahasa visual"-nya
    // nyambung sama energy spark ambient (TwinsAmbientFX) & rim glow Twins (TwinsRework.cs),
    // bukan ledakan generik bawaan game yang gaya-nya beda sendiri.
    //
    // Tiap 1 panggilan Spawn() = 1 "letusan" yang terdiri dari 3 lapis:
    //   1. Flash core putih-kuning (BloomFlare) - kilat sebentar, fade cepat.
    //   2. Shockwave ring (BloomCircleSmall) - ngembang cepat dari kecil, fade seiring ngembang.
    //   3. Beberapa ember debris (BloomCircleSmall kecil) - kepental nyebar, kena "gravity"
    //      ringan biar jatuh natural, meredup pelan-pelan.
    //
    // SENGAJA dipisah dari TwinsAmbientFX: beda "bahasa" (spark ambient = energi bocor halus
    // terus-menerus, ledakan ini = letusan besar sesaat) meski sama-sama numpang render
    // manual dari TwinsReworkOverride.PreDraw (lihat DrawExplosions() di bawah, dipanggil 1
    // baris tambahan di sana, PERSIS pola yang sama kayak DrawSparks - soalnya PreDraw Twins
    // return false, jadi GlobalNPC.PostDraw terpisah gak bakal pernah ke-panggil).
    //
    // Update() dipanggil dari TwinsLastStand.TickDeathAnimation() - AMAN dipanggil di situ
    // (bukan di PostAI kayak TwinsAmbientFX) karena TickDeathAnimation cuma jalan SEKALI per
    // tick (cuma lewat instance Spazmatism, si pemegang LastStandActive yang sebenarnya -
    // lihat komentar TwinsRework.CheckActive), jadi gak dobel-update kayak yang bisa kejadian
    // kalau dipanggil dari PostAI (yang jalan per-NPC, 2x per tick buat Retinazer+Spazmatism).
    // ==========================================
    public static class TwinsDeathExplosionFX
    {
        private struct Particle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float MaxScale; // target scale buat flash/ring; ember gak pakai ini
            public float Life;
            public float MaxLife;
            public Color Color;
            public float Rotation;
            public float RotationSpeed;
            public byte Kind; // 0 = flash core, 1 = shockwave ring, 2 = ember debris
        }

        private const int MaxParticles = 260; // cap keras, jaga-jaga kalau ledakan numpuk banyak

        private static readonly List<Particle> particles = new List<Particle>();

        // Digelapin dari versi sebelumnya (dulu ada nuansa kuning-terang) - sekarang full
        // "keluarga" oranye ke merah-tua, biar kesannya ledakan energi panas, bukan kembang api.
        private static readonly Color[] FireColors =
        {
            new Color(255, 140, 40),  // oranye
            new Color(220, 70, 30),   // oranye-merah
            new Color(140, 25, 20),   // merah gelap
        };

        // intensity: 1f = ledakan normal (dipakai death animation biasa), boleh dikecilin
        // (mis. 0.5f) buat variasi "letusan kecil" pas masih di udara (lihat pemanggil di
        // TwinsLastStand) biar gak semua letusan segede yang di darat.
        public static void Spawn(Vector2 position, float intensity = 1f)
        {
            if (Main.dedServ) // pure visual - server gak perlu nyimpen/proses particle ini
                return;

            // --- 1. FLASH CORE - kilat oranye terang kecil sebentar, fade cepat. Dulu putih-
            // kuning (kesannya kayak flash foto), sekarang digeser ke oranye biar tetap
            // "seleret" dengan tema api/panas ledakan, bukan kilat netral. ---
            particles.Add(new Particle
            {
                Position = position,
                Velocity = Vector2.Zero,
                Scale = 0.05f,
                MaxScale = 0.75f * intensity,
                Life = 0f,
                MaxLife = 12f,
                Color = new Color(255, 150, 60),
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotationSpeed = 0f,
                Kind = 0
            });

            // --- 2. SHOCKWAVE RING - ngembang cepat dari kecil, alpha turun seiring ngembang ---
            particles.Add(new Particle
            {
                Position = position,
                Velocity = Vector2.Zero,
                Scale = 0.05f,
                MaxScale = 1.5f * intensity,
                Life = 0f,
                MaxLife = 26f,
                Color = FireColors[1],
                Rotation = 0f,
                RotationSpeed = 0f,
                Kind = 1
            });

            // --- 3. EMBER DEBRIS - kepental nyebar radial, sedikit bias ke atas biar berasa
            // "kepental" bukan cuma nyebar rata, jatuh pelan-pelan (gravity ringan) ---
            int emberCount = Math.Max(3, (int)(Main.rand.Next(7, 12) * intensity));
            for (int i = 0; i < emberCount; i++)
            {
                if (particles.Count >= MaxParticles)
                    break;

                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float speed = Main.rand.NextFloat(1.5f, 5f) * intensity;
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                vel.Y -= Main.rand.NextFloat(0.5f, 2f);

                particles.Add(new Particle
                {
                    Position = position,
                    Velocity = vel,
                    Scale = Main.rand.NextFloat(0.08f, 0.15f) * MathHelper.Clamp(intensity, 0.6f, 1f),
                    MaxScale = 0f,
                    Life = 0f,
                    MaxLife = Main.rand.NextFloat(24f, 42f),
                    Color = FireColors[Main.rand.Next(FireColors.Length)],
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotationSpeed = Main.rand.NextFloat(-0.2f, 0.2f),
                    Kind = 2
                });
            }
        }

        // ==========================================
        // BUGFIX: particles disimpan di static List yang cuma di-Update() selama
        // TickDeathAnimation jalan. Begitu Twin resmi "mati" (npc.checkDead() di
        // TwinsLastStand.TickDeathAnimation), TickDeathAnimation berhenti dipanggil
        // SELAMANYA - kalau di tick itu masih ada particle yang belum habis Life-nya
        // (misal MaxLife-nya 42f tapi baru sempat Life ~20f pas kill manual kejadian),
        // particle itu beku permanen di posisi terakhir (gak pernah ke-Update lagi, gak
        // pernah ke-remove) dan bakal ke-render lagi pas Twins di-spawn ulang - soalnya
        // DrawExplosions() dipanggil dari PreDraw TIAP FRAME tanpa peduli fight lagi
        // aktif apa nggak, jadi list yang belum di-clear ya digambar apa adanya.
        // Dipanggil dari OnKill (TwinsRework.cs) pas Spazmatism BENERAN mati - itu momen
        // "source of truth" satu-satunya buat nutup fight - dan juga defensif dari
        // Trigger() (TwinsLastStand.cs) pas fight baru mulai, jaga-jaga kalau ada state
        // sisa yang somehow belum ke-clear dari fight sebelumnya.
        // ==========================================
        public static void Clear()
        {
            particles.Clear();
        }

        public static void Update()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.Life++;

                if (p.Life >= p.MaxLife)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                if (p.Kind == 2) // cuma ember yang beneran gerak/kena gravity ringan
                {
                    p.Position += p.Velocity;
                    p.Velocity.Y += 0.12f;
                    p.Velocity *= 0.985f;
                    p.Rotation += p.RotationSpeed;
                }

                particles[i] = p;
            }
        }

        // Dipanggil dari TwinsReworkOverride.PreDraw, sama pola persis kayak
        // TwinsAmbientFX.DrawSparks - additive blend, texture bloom ASLI Luminance.
        public static void DrawExplosions(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (particles.Count == 0)
                return;

            Texture2D circleTex = MiscTexturesRegistry.BloomCircleSmall.Value;
            Texture2D flareTex = MiscTexturesRegistry.BloomFlare.Value;
            if (circleTex == null || flareTex == null)
                return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (Particle p in particles)
            {
                float lifeRatio = p.Life / p.MaxLife;
                Vector2 drawPos = p.Position - screenPos;

                if (p.Kind == 0)
                {
                    // Flash core: gede cepat (3x lebih cepat dari total life-nya), fade linear.
                    float scale = MathHelper.Lerp(p.Scale, p.MaxScale, MathHelper.Clamp(lifeRatio * 3f, 0f, 1f));
                    float fade = 1f - lifeRatio;
                    Vector2 origin = new Vector2(flareTex.Width, flareTex.Height) * 0.5f;
                    spriteBatch.Draw(flareTex, drawPos, null, p.Color * fade, p.Rotation, origin, scale, SpriteEffects.None, 0f);
                }
                else if (p.Kind == 1)
                {
                    // Shockwave ring: ngembang terus penuh durasi, alpha turun bareng.
                    float scale = MathHelper.Lerp(p.Scale, p.MaxScale, lifeRatio);
                    float fade = 1f - lifeRatio;
                    Vector2 origin = new Vector2(circleTex.Width, circleTex.Height) * 0.5f;
                    spriteBatch.Draw(circleTex, drawPos, null, p.Color * (fade * 0.6f), p.Rotation, origin, scale, SpriteEffects.None, 0f);
                }
                else
                {
                    // Ember debris: fade in cepat, fade out pelan (sama filosofi kayak spark ambient).
                    float fade = lifeRatio < 0.2f ? lifeRatio / 0.2f : 1f - (lifeRatio - 0.2f) / 0.8f;
                    Vector2 origin = new Vector2(circleTex.Width, circleTex.Height) * 0.5f;
                    spriteBatch.Draw(circleTex, drawPos, null, p.Color * fade, p.Rotation, origin, p.Scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
