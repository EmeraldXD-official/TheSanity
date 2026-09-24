using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // "LUCILLE KARMA" TIER PROJECTILE VFX — foundation pass.
    // ================================================================================================
    // Applies to EVERY hostile projectile owned by the boss's dummy player (owner == proxySlot) -
    // mimicked player weapons, boss-native slashes (SpawnMeleeSlash / the new melee-archetype
    // helpers), and anything future archetype extras spawn the same way. Because this lives on the
    // GlobalProjectile (not per-attack code), new attacks get the full visual tier for free just by
    // spawning through the normal Projectile.NewProjectile(..., owner: proxySlot, ...) pipeline -
    // nobody has to hand-roll outline/noise/trail code per attack.
    //
    // Implements the "Lucille Karma" spec points, layered SpriteBatch/CPU passes as a fallback plus
    // a REAL GPU shader pass where available (WhoAmIProjectileEnergy.fx, see WhoAmI_VFX_ShaderSystem.cs
    // - TryDrawProjectileEnergy replaces DrawNeonOutlinePass() below automatically once that .fx is
    // compiled, no code here needs to change either way):
    //   1. HIGH-CONTRAST NEON OUTLINE   -> DrawNeonOutlinePass()        (CPU fallback: 8-directional
    //                                       offset silhouette) / WhoAmIProjectileEnergy.fx (GPU, when ready)
    //   2. CHROMATIC ABERRATION TRAILS  -> DrawChromaticTrail()         (manual oldPos strip, RGB-split,
    //                                       per-segment local direction so it doesn't flip/glitch when
    //                                       the projectile turns sharply - see fix comment there)
    //   3. SOFT ROUND BLOOM HALO        -> DrawWeaponTintedGlow()       (radial AuraGlow halo behind every
    //                                       projectile regardless of its own sprite shape)
    //   4. DYNAMIC LIGHTING             -> ApplyProjectileDynamicLight()(UPGRADE - Lighting.AddLight per
    //                                       projectile, tick-driven from PostAI same as the trail history,
    //                                       so every boss projectile actually lights its surroundings
    //                                       instead of just looking lit itself)
    //   5. IMPACT SHOCKWAVES            -> TriggerImpactShockwave()     (OnHitPlayer / OnKill hooks below)
    //
    // (The scrolling noise/turbulence pass that used to sit here was cut - it reused
    // WhoAmI_VFX.cs's AuraTurbulence asset and looked muddy/ugly on top of projectile sprites, so
    // it's gone rather than fixed.)
    // ================================================================================================
    public partial class WhoAmIProjectileGuard : GlobalProjectile
    {
        // Per-projectile trail history, since GlobalProjectile instances are shared/pooled by
        // vanilla rather than 1-per-projectile - use projectile.identity as the key so a slot reuse
        // (proj dies, new one spawns same array index next frame) doesn't inherit stale trail data.
        private static readonly System.Collections.Generic.Dictionary<int, Vector2[]> chromaticTrailHistory = new System.Collections.Generic.Dictionary<int, Vector2[]>();
        private const int ChromaticTrailLength = 10;

        // ---------------------------------------------------------------------------------------
        // 0) PATTERN-MARKER "STOP SPINNING + FLASH WHEN FIRING" HOOK
        // ---------------------------------------------------------------------------------------
        // Ties into WhoAmI_VFX_PatternMarker.cs's SignalPatternMarkerFire() - see the big comment on
        // that method for the full rationale. Fires automatically for every hostile, currently-
        // damaging projectile spawned via Projectile.NewProjectile(..., proxySlot) - which covers the
        // large majority of patterns (anything going through FireAttackProjectileAimed, plus every
        // pattern that calls Projectile.NewProjectile directly with real damage the moment it fires,
        // e.g. Gravity Well Torrent, Aureola Signet Rain, Orbiting Grid Lock, Singularity Overdrive,
        // Puppeteer's Snap, Mirror Lance Rupture). Excludes damage==0 spawns on purpose - those are
        // telegraph/prop projectiles (e.g. Orbiting Blade Ring's parked blades before they launch),
        // not an actual hit, and shouldn't trigger the "release" cue.
        //
        // Looks up the live WhoAmI instance rather than assuming a single static reference exists,
        // since none of the uploaded files declare one - safe even if that changes later, and cheap
        // (NPC.FindFirstNPC is an O(n) scan over active NPCs, called once per projectile spawn, not
        // once per tick).
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.owner != proxySlot) return;

            // FIX ("proyektil dari senjata ranger kadang ngebug jadi ungu-hitam kotak-kotak"):
            // PreDraw() di bawah manggil TextureAssets.Projectile[projectile.type].Value buat ambil
            // sprite-nya (dipakai bukan cuma buat gambar sprite dasarnya, tapi juga buat 3 layer VFX
            // tambahan - glow/outline/trail). Banyak proyektil senjata ranged (proyektil modded dari
            // mod lain, atau proyektil vanilla yang jarang kepakai di gameplay normal sehingga jarang
            // "dipicu" buat di-load) TIDAK di-force-load teksturnya di awal game - tModLoader/ReLogic
            // baru beneran nge-load asset itu ke memori pas PERTAMA KALI ada yang minta (lazy-load).
            // Kalau proyektil ini digambar SEBELUM proses load itu kelar, .Value buat sementara
            // balikin placeholder "missing texture" bawaan Terraria - kotak-kotak magenta/hitam -
            // padahal file .png/.rawimg aslinya ada dan valid, cuma belum sempat ke-load ke memori.
            // Begitu proyektil TIPE YANG SAMA muncul lagi setelahnya, cache-nya udah keisi dan
            // langsung normal - itu kenapa bug-nya kerasa "kadang doang", bukan tiap tembakan.
            //
            // Main.instance.LoadProjectile(type) adalah API resmi Terraria buat maksa nge-load
            // tekstur proyektil itu SEKARANG JUGA (synchronous), bukan nunggu draw call pertama yang
            // nemuin slotnya masih kosong. Manggilnya di OnSpawn - persis pas proyektil ini baru lahir,
            // SEBELUM dia sempat digambar sama sekali - motong race condition ini total, buat SEMUA
            // proyektil boss (bukan cuma yang hostile/damage>0 kayak sinyal pattern-marker di bawah),
            // termasuk proyektil "prop"/telegraph (damage 0) yang juga bisa kena bug yang sama.
            if (!Main.dedServ)
                Main.instance.LoadProjectile(projectile.type);

            if (!projectile.hostile || projectile.friendly || projectile.damage <= 0) return;

            int npcIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (npcIndex < 0 || npcIndex >= Main.maxNPCs) return;

            if (Main.npc[npcIndex].ModNPC is WhoAmI whoAmI)
                whoAmI.SignalPatternMarkerFire();
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.owner != proxySlot) return true;

            // FIX ("proyektil kadang ngebug" - chromatic trail suka nyambung/nge-streak dari titik
            // acak): trail history dulu di-update DI SINI, di PreDraw - tapi Terraria CULL PreDraw
            // buat proyektil yang lagi di luar layar (nggak dipanggil sama sekali kalau nggak
            // kelihatan kamera), sementara AI/posisi proyektil tetap jalan terus tiap tick walau
            // nggak digambar. Hasilnya: history array berhenti ke-update selama proyektil di luar
            // layar (boss dash/nge-dash jauh, orbiting blade muter lebar, dsb), lalu begitu dia
            // balik kelihatan, satu update tunggal nambahin posisi BARU di depan array yang isinya
            // masih posisi LAMA dari sebelum keluar layar - kebaca sebagai trail yang "nyambung"/
            // nge-streak melintasi jarak jauh dalam satu frame, bukan trail mulus. Sekarang
            // UpdateChromaticTrailHistory() dipanggil dari PostAI (WhoAmIProjectileGuard.cs) yang
            // jalan tiap game tick TANPA PEDULI kelihatan-nggaknya proyektil, jadi history-nya selalu
            // rapat/kontinu - PreDraw sekarang cuma BACA history yang udah ke-maintain di tempat lain.

            // Safety net matching the OnSpawn fix above - LoadProjectile() is a cheap no-op once the
            // texture is already loaded, so calling it again here costs nothing on the normal path,
            // but covers any projectile that somehow reaches PreDraw without going through OnSpawn
            // first (e.g. spawned by a helper that bypasses Projectile.NewProjectile's normal OnSpawn
            // call).
            if (!Main.dedServ)
                Main.instance.LoadProjectile(projectile.type);

            Texture2D tex = TextureAssets.Projectile[projectile.type].Value;
            if (tex == null) return true;

            SpriteBatch spriteBatch = Main.spriteBatch;

            Color weaponColor = GetWeaponCopyColor();
            Rectangle frame = Main.projFrames[projectile.type] > 1 ? tex.Frame(1, Main.projFrames[projectile.type], 0, projectile.frame) : tex.Bounds;
            Vector2 origin = frame.Size() * 0.5f;
            lightColor = weaponColor;

            // FIX (VFX/perf): each of the three sub-passes below used to open and close its OWN
            // additive/alpha-blend SpriteBatch pair independently - up to 3 End()/Begin() switches
            // PER PROJECTILE, PER FRAME (6 batch calls total once you count both the enter and exit
            // of each pass). With a dozen+ boss projectiles on screen at once (barrages, homing
            // clusters, ring blades) that's a real, avoidable draw-call/state-change cost every
            // single frame. It also meant DrawNeonOutlinePass ran in whatever blend mode the PREVIOUS
            // pass happened to leave active (regular alpha-blend, once DrawChromaticTrail restored
            // it) instead of additive - so the "glowing neon rim" it's named for was actually being
            // drawn as a flat, opaque silhouette offset, not a glow. Now we open ONE additive batch
            // for all three passes and let vanilla's own Begin() (right after this method returns)
            // handle the switch back to alpha-blend, instead of every helper doing it redundantly.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            DrawWeaponTintedGlow(spriteBatch, projectile, tex, frame, origin, weaponColor);

            // Fast-moving projectiles read as far more "unstable energy" with a visible chromatic
            // trail; slow/near-stationary ones (parked ring blades, orbiting yoyos) skip the trail
            // entirely so they don't smear into a static blur.
            // FIX ("vfx projectile kadang aneh" - trail suka pop/ilang mendadak): sebelumnya cuma
            // ngecek projectile.velocity TICK INI SAJA (>3f) buat mutusin gambar trail apa nggak.
            // Proyektil yang di-drive manual (whip/yoyo/boomerang di WhoAmIProjectileGuard.cs) sering
            // punya velocity yang jatuh ke ~0 sesaat walau baru aja gerak cepat (whip pas nyampe hold
            // phase, boomerang pas persis nyampe titik tangkap) - itu bikin trail yang harusnya masih
            // keliatan (history-nya masih penuh titik dari gerakan cepat barusan) malah LANGSUNG
            // ilang total di frame yang sama, kebaca sebagai "pop"/glitch. Sekarang juga dicek apakah
            // history yang udah ke-rekam masih py spread yang berarti (proyektil beneran baru diam,
            // bukan cuma kebetulan velocity instannya nol sesaat) - trail-nya jadi fade out natural
            // ngikutin ChromaticTrailLength tick, bukan putus mendadak.
            float speed = projectile.velocity.Length();
            bool showTrail = speed > 3f;
            if (!showTrail && chromaticTrailHistory.TryGetValue(projectile.identity, out Vector2[] recentHistory))
            {
                for (int i = 0; i < recentHistory.Length; i++)
                {
                    if (float.IsNaN(recentHistory[i].X)) continue;
                    if (Vector2.DistanceSquared(recentHistory[i], projectile.Center) > 16f) { showTrail = true; break; }
                }
            }
            if (showTrail)
                DrawChromaticTrail(spriteBatch, projectile, tex, frame, origin);

            // REAL PER-PIXEL RIM (WhoAmIProjectileEnergy.fx, see WhoAmI_VFX_ShaderSystem.cs) - single
            // GPU draw call that replaces the 8-direction CPU redraw below when the shader has been
            // compiled. Falls back to DrawNeonOutlinePass() automatically otherwise, so nothing here
            // needs to change based on whether you've built the .fx yet.
            Vector2 drawPosForShader = projectile.Center - Main.screenPosition;
            Color neonColor = GetArchetypeNeonColor(projectile);
            bool drewShaderRim = WhoAmIShaderSystem.TryDrawProjectileEnergy(spriteBatch, tex, frame, drawPosForShader,
                origin, projectile.rotation, projectile.scale, neonColor, weaponColor, 1f);

            if (!drewShaderRim)
                DrawNeonOutlinePass(spriteBatch, projectile, tex, frame, origin, lightColor);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return true; // let vanilla draw the crisp base sprite on top of our outline/noise layers
        }

        // ---------------------------------------------------------------------------------------
        // 1) HIGH-CONTRAST NEON OUTLINE
        // ---------------------------------------------------------------------------------------
        // Classic "8-directional offset silhouette" outline trick: draw the projectile's own sprite
        // N times at a small pixel radius around its true position, tinted solid neon/white, BEFORE
        // vanilla draws the real sprite on top. Reads as a crisp glowing rim around every projectile
        // regardless of its base art, with zero new texture assets.
        private static Color GetWeaponCopyColor()
        {
            if (Main.player[proxySlot] == null) return Color.White;
            Player dummy = Main.player[proxySlot];
            if (dummy.inventory == null || dummy.inventory.Length == 0) return Color.White;
            Item weapon = dummy.inventory[0];
            if (weapon == null || weapon.IsAir) return Color.White;
            if (weapon.CountsAsClass(DamageClass.Melee) || (weapon.shoot > 0 && ProjectileID.Sets.IsAWhip[weapon.shoot]))
                return new Color(255, 90, 70);
            if (weapon.CountsAsClass(DamageClass.Ranged))
                return new Color(110, 230, 130);
            if (weapon.CountsAsClass(DamageClass.Magic))
                return new Color(170, 100, 255);
            if (weapon.CountsAsClass(DamageClass.Summon))
                return new Color(255, 200, 80);
            return Color.White;
        }

        // Halo bulat LEMBUT (bukan cuma silhouette sprite proyektil-nya sendiri yang di-scale up) -
        // sebelumnya "glow" di sini cuma re-draw sprite proyektil itu sendiri jadi transparan, yang
        // buat proyektil non-bulat (panah, bilah, dsb) kebaca aneh/kotak, bukan glow beneran. Numpang
        // aset AuraGlow yang sama kayak WhoAmI_VFX.cs (radial soft glow, edge diperturbasi noise) -
        // di-request sendiri di sini (bukan lewat field private WhoAmI, beda class/GlobalProjectile)
        // supaya tiap proyektil punya bloom bulat yang lembut di baliknya, appropriate buat SEMUA
        // bentuk sprite, baru DI ATASnya lapisan silhouette-tinted yang lama.
        private static Asset<Texture2D> projectileHaloTexture;
        private static void EnsureHaloTextureLoaded()
        {
            projectileHaloTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlow", AssetRequestMode.ImmediateLoad);
        }

        // FIX ("proyektil peluru masih ngebug jadi kotak-kotak pink gede, meski tipe proyektil &
        // texture-load-nya udah dibenerin di 2 fix sebelumnya"): berarti sumbernya BUKAN lagi
        // projectile.type/texture proyektilnya sendiri (tex, di-force-load lewat OnSpawn - sudah
        // benar), tapi aset "AuraGlow" TERPISAH yang di-request di EnsureHaloTextureLoaded() di
        // bawah - satu-satunya hal di layer ini yang bukan tekstur proyektil asli. Lihat
        // IsSimpleBulletProjectile() di bawah buat scope-nya.
        private static bool IsSimpleBulletProjectile(int type) =>
            type == ProjectileID.Bullet || type == ProjectileID.BulletHighVelocity;

        private static void DrawWeaponTintedGlow(SpriteBatch spriteBatch, Projectile projectile, Texture2D tex, Rectangle frame, Vector2 origin, Color weaponColor)
        {
            Vector2 drawPos = projectile.Center - Main.screenPosition;
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.6f + projectile.identity * 0.2f);
            float speed = projectile.velocity.Length();
            float trailScale = projectile.scale * MathHelper.Lerp(1f, 1.25f, MathHelper.Clamp(speed / 16f, 0f, 1f));

            // Daripada terus nebak apa yang salah sama file AuraGlow itu (path/compile/dsb - nggak
            // bisa dicek dari sini tanpa akses langsung ke game-nya), khusus utk peluru simpel ini
            // SKIP TOTAL layer berbasis tekstur AuraGlow, ganti pakai partikel spark tema-warna
            // (LuminanceUtilities - sistem partikel TERPISAH yang udah kepake aman di banyak tempat
            // lain di file ini: TriggerImpactShockwave, TickHitFlash, dst - tanpa pernah ada laporan
            // bug serupa). Peluru tetap dapet nuansa "menyala neon tema-warna", tapi nggak lagi
            // gantung ke aset yang kebetulan bermasalah itu. Weapon lain (blade, orb sihir, dsb)
            // TETAP pakai halo AuraGlow seperti biasa - scope fix ini sengaja dipersempit ke peluru
            // doang sesuai laporan bug-nya.
            if (IsSimpleBulletProjectile(projectile.type))
            {
                // Throttled ke tiap 2 tick - PreDraw jalan tiap frame proyektil kelihatan, tanpa ini
                // bakal spam partikel berlebihan buat peluru cepat/barrage banyak sekaligus.
                if (Main.GameUpdateCount % 2 == 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 jitter = Main.rand.NextVector2Circular(3f, 3f);
                        Vector2 vel = -projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f);
                        LuminanceUtilities.SpawnParticle(projectile.Center + jitter, vel, weaponColor, 14, 0.55f * pulse, ParticleType.Spark);
                    }
                }
            }
            else
            {
                EnsureHaloTextureLoaded();
                if (projectileHaloTexture?.Value != null)
                {
                    Texture2D halo = projectileHaloTexture.Value;
                    Vector2 haloOrigin = new Vector2(halo.Width / 2f, halo.Height / 2f);
                    float haloScale = (frame.Width + frame.Height) / 2f / halo.Width * 2.2f * (0.85f + 0.15f * pulse);
                    spriteBatch.Draw(halo, drawPos, null, weaponColor * 0.30f * pulse, 0f, haloOrigin, haloScale, SpriteEffects.None, 0f);
                    spriteBatch.Draw(halo, drawPos, null, Color.White * 0.10f * pulse, 0f, haloOrigin, haloScale * 0.5f, SpriteEffects.None, 0f);
                }
            }

            // (Batch is already open in additive mode - see PreDraw - no local Begin/End needed here anymore.)
            spriteBatch.Draw(tex, drawPos, frame, weaponColor * 0.2f * pulse, projectile.rotation, origin, projectile.scale * 1.1f, SpriteEffects.None, 0f);
            if (speed > 3f)
            {
                for (int i = 2; i <= 5; i++)
                {
                    float t = i / 6f;
                    spriteBatch.Draw(tex, drawPos - projectile.velocity * t * 0.1f, frame, weaponColor * 0.08f * (1f - t) * pulse, projectile.rotation, origin, trailScale * (1f + t * 0.08f), SpriteEffects.None, 0f);
                }
            }

            float noiseRadius = 2f + 1.5f * (float)Math.Sin(Main.GameUpdateCount * 0.35f + projectile.identity * 0.2f);
            for (int i = 0; i < 3; i++)
            {
                float ang = Main.GameUpdateCount * 0.18f + i * MathHelper.TwoPi / 3f;
                Vector2 offset = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * noiseRadius;
                spriteBatch.Draw(tex, drawPos + offset, frame, weaponColor * 0.06f * pulse, projectile.rotation, origin, projectile.scale * 1.05f, SpriteEffects.None, 0f);
            }
        }

        private static void DrawNeonOutlinePass(SpriteBatch spriteBatch, Projectile projectile, Texture2D tex, Rectangle frame, Vector2 origin, Color lightColor)
        {
            Vector2 drawPos = projectile.Center - Main.screenPosition;
            Color neon = GetArchetypeNeonColor(projectile);
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.4f + projectile.identity);
            const int offsetPx = 2;
            const int directions = 8;

            for (int i = 0; i < directions; i++)
            {
                float ang = MathHelper.TwoPi * i / directions;
                Vector2 offset = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * offsetPx;
                spriteBatch.Draw(tex, drawPos + offset, frame, neon * pulse, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0f);
            }
        }

        // ---------------------------------------------------------------------------------------
        // 2) CHROMATIC ABERRATION TRAILS
        // ---------------------------------------------------------------------------------------
        // Manual oldPos-style strip (rather than assuming a specific Luminance PrimitiveRenderer
        // overload signature that may differ between Luminance versions): samples our own tracked
        // position history and draws 3 slightly-offset, color-channel-tinted copies (red pushed
        // back along the velocity, cyan pushed forward) that fade out - the RGB-split reads clearly
        // at the high velocities these attacks actually move at.
        private static void DrawChromaticTrail(SpriteBatch spriteBatch, Projectile projectile, Texture2D tex, Rectangle frame, Vector2 origin)
        {
            if (!chromaticTrailHistory.TryGetValue(projectile.identity, out Vector2[] history)) return;

            // FIX ("vfx projectile kadang aneh" - trail suka "kebalik"/glitch pas proyektil belok
            // tajam): sebelumnya SATU arah global (projectile.velocity di tick SEKARANG) dipakai buat
            // nge-offset SEMUA titik history sekaligus. Titik2 history itu posisi LAMA yang sering
            // gerak ke arah BEDA dari tick sekarang - paling kentara buat proyektil yang di-drive
            // manual (whip narik balik, boomerang pindah dari fase "keluar" ke "homing", yoyo muter)
            // di WhoAmIProjectileGuard.cs, yang arahnya bisa berubah tajam antar tick. Begitu arah
            // sekarang beda jauh dari arah histori lama, SELURUH trail "flip" arah RGB-split-nya
            // dalam satu frame - kebaca sebagai glitch/pop yang aneh, bukan trail yang mulus.
            //
            // Fix: tiap segmen history sekarang pakai arah LOKALnya sendiri (dari titik itu ke titik
            // tetangganya yang lebih tua), bukan satu arah global - trail-nya jadi selalu ngikutin
            // lekukan lintasan aslinya secara mulus, apapun seberapa tajam proyektilnya belok.
            Vector2 fallbackDir = projectile.velocity.SafeNormalize(Vector2.UnitX);

            for (int i = 0; i < history.Length; i++)
            {
                if (float.IsNaN(history[i].X)) continue;
                float t = i / (float)history.Length;
                float alpha = (1f - t) * 0.5f;
                if (alpha <= 0.01f) continue;

                Vector2 newer = i == 0 ? projectile.Center : history[i - 1];
                Vector2 older = (i < history.Length - 1 && !float.IsNaN(history[i + 1].X)) ? history[i + 1] : history[i];
                Vector2 segment = newer - older;
                Vector2 dir = segment.LengthSquared() > 0.01f ? segment.SafeNormalize(fallbackDir) : fallbackDir;
                Vector2 perp = new Vector2(-dir.Y, dir.X);

                Vector2 basePos = history[i] - Main.screenPosition;
                float splitPx = MathHelper.Lerp(0f, 5f, 1f - t);

                spriteBatch.Draw(tex, basePos - dir * splitPx, frame, Color.Red * alpha * 0.55f, projectile.rotation, origin, projectile.scale * MathHelper.Lerp(1f, 0.6f, t), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos + dir * splitPx, frame, Color.Cyan * alpha * 0.55f, projectile.rotation, origin, projectile.scale * MathHelper.Lerp(1f, 0.6f, t), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos + perp * splitPx * 0.4f, frame, Color.White * alpha * 0.35f, projectile.rotation, origin, projectile.scale * MathHelper.Lerp(1f, 0.6f, t), SpriteEffects.None, 0f);
            }
        }

        // Called from WhoAmIProjectileGuard.cs's PostAI (same partial class) instead of from
        // PreDraw - see the FIX comment in PreDraw above for why. "internal" (not private) so the
        // other file can call it directly without needing a public API surface.
        internal static void UpdateChromaticTrailHistory(Projectile projectile)
        {
            if (!chromaticTrailHistory.TryGetValue(projectile.identity, out Vector2[] history))
            {
                history = new Vector2[ChromaticTrailLength];
                // FIX (minor): slots used to default to Vector2.Zero and DrawChromaticTrail treated
                // Zero as "empty/unused". A boss projectile that happens to pass near actual world
                // coordinate (0,0) would have a real trail point silently skipped. NaN can never be
                // a projectile's real position, so it's an unambiguous "not filled yet" sentinel.
                for (int i = 0; i < history.Length; i++) history[i] = new Vector2(float.NaN, float.NaN);
                chromaticTrailHistory[projectile.identity] = history;
            }
            for (int i = history.Length - 1; i > 0; i--) history[i] = history[i - 1];
            history[0] = projectile.Center;
        }

        // FIX (outline was always flat white despite this comment claiming otherwise): the previous
        // implementation just returned a hardcoded neutral color and never actually looked at
        // anything about the projectile, so every single boss projectile - melee, ranged, magic,
        // summon - got the exact same outline tint, undercutting the "per-attack identity" goal the
        // rest of this VFX pass otherwise cares about (see WhoAmI_VFX_Attacks.cs's per-aiState tint).
        // GetWeaponCopyColor() (right above) already derives a weapon-archetype color purely from the
        // dummy player's held item - no boss aiState needed - and DrawWeaponTintedGlow already uses it
        // for the glow layer, so the outline just needed to reuse it instead of hardcoding white.
        // Blended partway toward white so the outline still reads as a bright "neon rim" rather than
        // just a dim recolor of the glow underneath it.
        private static Color GetArchetypeNeonColor(Projectile projectile)
        {
            Color weaponColor = GetWeaponCopyColor();
            return Color.Lerp(weaponColor, Color.White, 0.45f);
        }

        // ---------------------------------------------------------------------------------------
        // 4) DYNAMIC LIGHTING
        // ---------------------------------------------------------------------------------------
        // UPGRADE ("pencahayaan projectile"): sebelumnya boss badannya sendiri sudah punya dynamic
        // light yang jalan terus (Lighting.AddLight di WhoAmI_VFX.cs), tapi PROYEKTIL yang dia
        // tembakin nol dynamic light sama sekali - walau sprite-nya udah full glow-stack (halo +
        // outline + trail di atas), area SEKITAR proyektilnya sendiri tetap gelap total kalau lagi
        // di gua/underground gelap. Kesannya jadi "sprite yang keliatan nyala" doang, bukan beneran
        // sumber cahaya yang mancar ke lingkungannya - beda jauh sama gimana boss badannya sendiri
        // udah "nge-tint" area sekitarnya. Warna ngikut GetWeaponCopyColor() yang sama kayak dipakai
        // buat halo/outline/trail di atas, jadi pencahayaannya konsisten sama warna visual proyektil-
        // nya sendiri, bukan warna generik terpisah.
        internal static void ApplyProjectileDynamicLight(Projectile projectile)
        {
            if (Main.dedServ) return;

            Color weaponColor = GetWeaponCopyColor();
            // Pulsa cepat & ringan, senada breathing sprite-nya sendiri (DrawWeaponTintedGlow di
            // bawah pakai frekuensi yang sama: 0.6f + identity offset) - biar cahayanya "berdenyut"
            // bareng sama glow yang keliatan, bukan dua ritme lepas yang nggak nyambung.
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.6f + projectile.identity * 0.2f);
            // Proyektil yang lagi ngebut kerasa lebih "energik"/terang dari yang pelan/parkir (ring
            // blade nunggu, yoyo di orbit) - sama filosofinya kayak DrawWeaponTintedGlow yang juga
            // nge-scale trail berdasar speed.
            float speedBoost = MathHelper.Clamp(projectile.velocity.Length() / 20f, 0f, 1f);
            float intensity = (0.5f + speedBoost * 0.35f) * pulse * MathHelper.Clamp(projectile.scale, 0.4f, 2.5f);

            Lighting.AddLight(projectile.Center, weaponColor.ToVector3() * intensity);
        }

        // ---------------------------------------------------------------------------------------
        // 5) IMPACT SHOCKWAVES & SCREEN DISTORTION
        // ---------------------------------------------------------------------------------------
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.owner == proxySlot)
                TriggerImpactShockwave(projectile.Center, info.Damage > 40 ? 1.15f : 0.7f);
        }

        // FIX (screenshake spam / "banyak proyektil bug"): this used to fire a full shockwave+shake
        // for EVERY fast-moving boss projectile that died, with no distinction between "actually hit
        // something" and "just ran out of time in open air" (a barrage/cluster shot that never touched
        // anything still dies moving fast). Worse, the manual yoyo/boomerang/whip logic in
        // WhoAmIProjectileGuard.cs ALWAYS ends in an explicit projectile.Kill() once it finishes
        // retracting to the boss - that's pure housekeeping, not an impact, and it was never actually
        // excluded despite the old comment here claiming it was. Two changes:
        //   1. aiStyle Yoyo/Boomerang/Whip are excluded outright - those never represent a real hit.
        //   2. A short global cooldown caps how often a shockwave can fire at all, so a barrage/comet
        //      pattern whose projectiles all expire within the same few ticks can't stack a dozen
        //      shockwaves (and shakes) on top of each other.
        // Uses Main.GameUpdateCount rather than a manually-decremented counter since GlobalProjectile
        // has no reliable "once per game tick, regardless of how many projectiles exist" hook to
        // decrement a counter in - comparing against the game's own tick counter needs no such hook.
        private static long lastImpactShockwaveTick = -1000;
        private const int ImpactShockwaveCooldownTicks = 4;

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            bool isManualRetractStyle = projectile.aiStyle == ProjAIStyleID.Yoyo || projectile.aiStyle == ProjAIStyleID.Boomerang || projectile.aiStyle == ProjAIStyleID.Whip;
            bool offCooldown = Main.GameUpdateCount - lastImpactShockwaveTick >= ImpactShockwaveCooldownTicks;

            if (projectile.owner == proxySlot && !isManualRetractStyle && projectile.velocity.LengthSquared() > 16f && offCooldown)
            {
                TriggerImpactShockwave(projectile.Center, 0.6f);
                lastImpactShockwaveTick = (long)Main.GameUpdateCount;
            }

            chromaticTrailHistory.Remove(projectile.identity);
        }

        private static void TriggerImpactShockwave(Vector2 position, float intensity)
        {
            ScreenShakeSystem.StartShakeAtPoint(position, 6f * intensity, 0.25f * intensity);

            for (int i = 0; i < 16; i++)
            {
                float ang = MathHelper.TwoPi * i / 16f;
                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                LuminanceUtilities.SpawnParticle(position + dir * 6f, dir * 5f * intensity, Color.White, 14, 1f * intensity, ParticleType.Spark);
            }
            for (int i = 0; i < 10; i++)
                LuminanceUtilities.SpawnParticle(position, Main.rand.NextVector2Circular(3f, 3f), new Color(235, 245, 255), 20, 1.3f * intensity, ParticleType.Spark);
        }
    }
}