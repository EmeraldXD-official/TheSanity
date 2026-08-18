using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Assets;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // TwinsAmbientFX — VISUAL UPGRADE, PURE KOSMETIK. Sistem "energy spark" SENDIRI (ganti
    // dari Dust vanilla yang kepentok kotak/kasar) - dirender pakai asset BLOOM ASLI dari
    // Luminance (MiscTexturesRegistry.BloomCircleSmall / BloomFlare), biar nyambung sama
    // "bahasa visual" bloom yang udah dipakai di beam-beam Twins (BeamGlowTexture dkk),
    // bukan gaya baru yang beda sendiri.
    //
    // SCOPE (request): DULU nyala sepanjang fight (dari awal sampai akhir), lalu sempat jadi
    // "dari Trigger() sampai sebelum DeathAnimation". SEKARANG jendelanya digeser lagi: BARU
    // nyala begitu HP-nya BENERAN di-refill ke 100% (persis abis Roar + 2 chat text "SYSTEM
    // OVERLOAD" kelar - lihat TwinsLastStand.State.RoarAndRefill) - BUKAN dari awal Trigger()
    // lagi. Jadi selama Twin masih terbang ke tengah/nunggu kedua Spectre clone tumbang
    // (MovingToCenter/HoldForSpectres) DAN selama jeda diam+Roar (RoarAndRefill, sebelum
    // refill kejadian), glow ini WAJIB padam - baru nyala PERSIS bareng momen HP balik 100%,
    // dan tetap nyala sampai SEBELUM DeathAnimation (jatuh+ledakan) mulai, jadi "berhenti"
    // tepat begitu Deathray (beam terakhir) kelar. Di luar window itu (termasuk
    // Enraged/Phase 3 normal di luar Last Stand) spark ini DIMATIKAN TOTAL - biarin fokus
    // visual ke ledakan (TwinsDeathExplosionFX) pas death animation. Dicek lewat
    // GetSpazmatismStateOrNull() + TwinsLastStand.IsActiveBeforeDeathAnimation() di bawah,
    // WAJIB nengok instance GlobalNPC milik Spazmatism (bukan npc ini sendiri) -
    // LastStandActive itu field per-instance yang cuma di-Set/berarti dari sisi Spazmatism
    // (lihat komentar TwinsRework.CheckActive), instance Retinazer sendiri SELALU false.
    //
    // SENGAJA dipisah dari TwinsReworkOverride: spawn+update spark di sini lewat PostAI
    // (GlobalNPC hook BARU, GAK NYENTUH AI/pattern Twins manapun - cuma BACA state Last
    // Stand buat nentuin nyala/mati & intensitas). Tapi GAMBAR-nya WAJIB dipanggil dari
    // TwinsReworkOverride.PreDraw (lihat DrawSparks() di bawah, dipanggil 1 baris tambahan
    // di sana) - soalnya PreDraw Twins return false (draw vanilla dibatalin total), jadi
    // GlobalNPC.PostDraw TERPISAH gak bakal pernah ke-panggil buat Retinazer/Spazmatism.
    // ==========================================
    public class TwinsAmbientFX : GlobalNPC
    {
        private struct Spark
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Scale;
            public float MaxLife;
            public float Life;
            public Color Color;
            public bool UseFlareShape; // gantian BloomCircleSmall (bulat) / BloomFlare (silang) biar variatif
        }

        private const int MaxSparks = 220; // cap keras - jaga-jaga biar gak numpuk gak jelas kalau lag
        private static readonly List<Spark> sparks = new List<Spark>();

        private const int EmberIntervalTicks = 3; // spawn tiap ~0.05 detik per Twin

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.Retinazer && npc.type != NPCID.Spazmatism)
                return;

            if (Main.dedServ) // partikel murni visual, server gak perlu proses ini sama sekali
                return;

            UpdateSparks();

            // SCOPE: cuma boleh SPAWN spark baru selama window Desperate Phase (lihat
            // komentar di atas). UpdateSparks() di atas TETAP jalan tiap tick gak peduli
            // window ini aktif atau nggak, biar spark yang UDAH ada sempat fade-out wajar
            // (bukan ilang mendadak) begitu window-nya tutup.
            TwinsReworkOverride spazState = GetSpazmatismStateOrNull();
            if (spazState == null || !TwinsLastStand.IsActiveBeforeDeathAnimation(spazState))
                return;

            if (Main.GameUpdateCount % EmberIntervalTicks != 0)
                return;

            // Intensitas NAIK pas Deathray (last beam) lagi nyala - murni kosmetik, cuma BACA
            // flag publik ini, gak pernah nulis apapun ke situ. IsEnraged gak perlu dicek lagi
            // di sini - selama window Desperate Phase aktif, IsEnraged pasti udah true (lihat
            // TwinsLastStand.cs), jadi burst dasarnya sendiri udah "versi enraged".
            int burstCount = 2;
            if (spazState.LastStandDeathrayActive) burstCount++;

            // Merah-oranye buat Retinazer (senada mata/laser-nya), hijau-lime buat
            // Spazmatism (senada tema after-image trail-nya yang udah ada).
            Color emberColor = npc.type == NPCID.Retinazer ? new Color(255, 90, 50) : new Color(100, 255, 110);

            for (int i = 0; i < burstCount; i++)
            {
                if (sparks.Count >= MaxSparks)
                    break;

                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(npc.width * 0.15f, npc.width * 0.42f);
                Vector2 spawnPos = npc.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;

                // Melayang pelan ke atas dengan sedikit sebaran arah, bukan lurus kaku.
                float velAngle = -MathHelper.PiOver2 + Main.rand.NextFloat(-0.6f, 0.6f);
                float velSpeed = Main.rand.NextFloat(0.4f, 1.3f);
                Vector2 velocity = new Vector2((float)Math.Cos(velAngle), (float)Math.Sin(velAngle)) * velSpeed;

                sparks.Add(new Spark
                {
                    Position = spawnPos,
                    Velocity = velocity,
                    Scale = Main.rand.NextFloat(0.09f, 0.16f),
                    MaxLife = Main.rand.NextFloat(35f, 55f),
                    Life = 0f,
                    Color = emberColor,
                    UseFlareShape = Main.rand.NextBool()
                });
            }
        }

        // Cari instance GlobalNPC milik Spazmatism - "sumber kebenaran" satu-satunya buat
        // status Last Stand (LastStandActive/LastStandStateRaw/LastStandDeathrayActive),
        // gak peduli npc yang manggil PostAI ini Retinazer atau Spazmatism sendiri. Null
        // kalau Spazmatism kebetulan lagi gak ada/gak aktif.
        private static TwinsReworkOverride GetSpazmatismStateOrNull()
        {
            int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
            if (spazIndex == -1 || !Main.npc[spazIndex].active)
                return null;

            return Main.npc[spazIndex].GetGlobalNPC<TwinsReworkOverride>();
        }

        // ==========================================
        // Simetri sama TwinsDeathExplosionFX.Clear(): sparks juga static List. Secara teori
        // TwinsAmbientFX udah aman sendirian (window Desperate Phase-nya nutup jauh sebelum
        // DeathAnimation kelar, jadi UpdateSparks() sempat ngosongin list lewat fade-out
        // wajar sebelum npc.checkDead() kejadian) - tapi tetap disediain Clear() sebagai
        // lapisan pengaman kalau ada edge case (mis. Twin di-despawn paksa/dibunuh command
        // di tengah window aktif, exception, dll) yang bikin sparks nyangkut.
        // ==========================================
        public static void Clear()
        {
            sparks.Clear();
        }

        private static void UpdateSparks()
        {
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                Spark s = sparks[i];
                s.Life++;
                s.Position += s.Velocity;
                s.Velocity *= 0.98f; // pelan-pelan berhenti, gak lurus kaku sampai mati

                if (s.Life >= s.MaxLife)
                {
                    sparks.RemoveAt(i);
                    continue;
                }

                sparks[i] = s;
            }
        }

        // Dipanggil dari TwinsReworkOverride.PreDraw - render SEMUA spark aktif di titik ini,
        // pakai asset bloom ASLI Luminance, additive, fade in cepat/fade out pelan biar gak
        // "pop" tiba-tiba muncul/ilang.
        public static void DrawSparks(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (sparks.Count == 0)
                return;

            Texture2D circleTex = MiscTexturesRegistry.BloomCircleSmall.Value;
            Texture2D flareTex = MiscTexturesRegistry.BloomFlare.Value;
            if (circleTex == null || flareTex == null)
                return;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (Spark s in sparks)
            {
                float lifeRatio = s.Life / s.MaxLife;
                float fade = lifeRatio < 0.15f ? lifeRatio / 0.15f : 1f - (lifeRatio - 0.15f) / 0.85f;

                Texture2D tex = s.UseFlareShape ? flareTex : circleTex;
                Vector2 origin = new Vector2(tex.Width, tex.Height) * 0.5f;
                Vector2 drawPos = s.Position - screenPos;

                spriteBatch.Draw(tex, drawPos, null, s.Color * fade, 0f, origin, s.Scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
