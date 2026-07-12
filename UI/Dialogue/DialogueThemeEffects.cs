using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// Jenis efek partikel ambient yang bisa ditempel ke sebuah DialogueTheme (lewat DialogueTheme.Effect).
    /// Ganti tema = ganti efek otomatis (renderer nge-detect perubahan Kind sendiri & bersihin partikel lama).
    /// </summary>
    public enum ThemeEffectKind
    {
        None,
        FallingLeaves,  // Green Forest - daun jatuh melayang
        BloodDrip,      // Blood - tetesan darah jatuh dari atas
        Embers,         // Yellow & Orange Sunset - percikan bara naik
        Bubbles,        // Blue Ocean - gelembung naik
        Snowfall,       // White Moon - salju jatuh pelan
        Sparkle,        // Purple Royal - kerlip bintang kecil
        NeonGlitch,     // Cyber Neon - glitch garis neon
        SandDrift,      // Desert Sand - pasir tertiup dari kiri ke kanan
        StaticNoise,    // Dark Retro - kerlip noise ala TV tabung retro
    }

    /// <summary>Data 1 partikel efek (posisi, gerak, umur, ukuran). Internal, ga perlu diutak-atik dari luar.</summary>
    internal struct ThemeParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float MaxLife;
        public float Width;
        public float Height;
        public float Sway;
        public float SwaySpeed;
        public float Seed;
    }

    /// <summary>
    /// Renderer efek ambient per-tema. Digambar pakai MagicPixel (kotak kecil) doang, jadi ga
    /// butuh asset gambar tambahan. Efek ini PURE VISUAL - ga ngaruh sama sekali ke data dialog,
    /// timer, atau history. Cukup panggil Update() tiap frame lalu Draw() saat mau digambar;
    /// otomatis ganti "jenis efek" & bersihin partikel lama begitu ThemeEffectKind yang dikasih beda
    /// dari sebelumnya (misal player pindah baris yang temanya beda, atau tema di-force ganti).
    /// </summary>
    public class ThemeEffectRenderer
    {
        private readonly List<ThemeParticle> _particles = new List<ThemeParticle>();
        private readonly Random _rng = new Random();
        private ThemeEffectKind _currentKind = ThemeEffectKind.None;
        private float _spawnAccumulator = 0f;

        public void Update(float dt, ThemeEffectKind kind, Rectangle bounds)
        {
            if (kind != _currentKind)
            {
                _particles.Clear();
                _currentKind = kind;
                _spawnAccumulator = 0f;
            }

            if (kind == ThemeEffectKind.None || bounds.Width <= 0 || bounds.Height <= 0) return;

            float spawnRate = GetSpawnRate(kind);
            _spawnAccumulator += dt * spawnRate;
            while (_spawnAccumulator >= 1f)
            {
                _spawnAccumulator -= 1f;
                if (_particles.Count < 70) _particles.Add(SpawnParticle(kind, bounds));
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                ThemeParticle p = _particles[i];
                p.Life += dt;
                AdvanceParticle(ref p, kind, dt);
                if (p.Life >= p.MaxLife) _particles.RemoveAt(i);
                else _particles[i] = p;
            }
        }

        public void Draw(SpriteBatch sb, ThemeEffectKind kind, DialogueTheme theme, float globalOpacity)
        {
            if (kind == ThemeEffectKind.None || globalOpacity <= 0f || _particles.Count == 0) return;
            Texture2D px = TextureAssets.MagicPixel.Value;

            foreach (ThemeParticle p in _particles)
            {
                float lifeRatio = p.MaxLife <= 0f ? 0f : MathHelper.Clamp(p.Life / p.MaxLife, 0f, 1f);
                float fade = GetFadeAlpha(kind, lifeRatio);
                if (fade <= 0f) continue;

                Color color = GetParticleColor(kind, theme);
                int w = Math.Max(1, (int)p.Width);
                int h = Math.Max(1, (int)p.Height);
                var rect = new Rectangle((int)p.Position.X, (int)p.Position.Y, w, h);
                sb.Draw(px, rect, color * fade * globalOpacity);
            }
        }

        private float GetSpawnRate(ThemeEffectKind kind)
        {
            switch (kind)
            {
                case ThemeEffectKind.FallingLeaves: return 2.0f;
                case ThemeEffectKind.BloodDrip: return 1.1f;
                case ThemeEffectKind.Embers: return 3.2f;
                case ThemeEffectKind.Bubbles: return 2.4f;
                case ThemeEffectKind.Snowfall: return 3.2f;
                case ThemeEffectKind.Sparkle: return 3.6f;
                case ThemeEffectKind.NeonGlitch: return 2.6f;
                case ThemeEffectKind.SandDrift: return 2.8f;
                case ThemeEffectKind.StaticNoise: return 5.5f;
                default: return 0f;
            }
        }

        private ThemeParticle SpawnParticle(ThemeEffectKind kind, Rectangle b)
        {
            var p = new ThemeParticle { Life = 0f };
            float Rx() => (float)_rng.NextDouble();

            switch (kind)
            {
                case ThemeEffectKind.FallingLeaves:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Y - 6);
                    p.Velocity = new Vector2(0f, 16f + Rx() * 14f);
                    p.Sway = 10f + Rx() * 16f;
                    p.SwaySpeed = 1.4f + Rx() * 1.6f;
                    p.Width = 4f + Rx() * 3f;
                    p.Height = 3f + Rx() * 2f;
                    p.MaxLife = b.Height / p.Velocity.Y + 0.4f;
                    break;

                case ThemeEffectKind.BloodDrip:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Y + 2);
                    p.Velocity = new Vector2(0f, 46f + Rx() * 34f);
                    p.Width = 2f + Rx() * 1.5f;
                    p.Height = 5f + Rx() * 4f;
                    p.MaxLife = b.Height / p.Velocity.Y + 0.25f;
                    break;

                case ThemeEffectKind.Embers:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Bottom - 4);
                    p.Velocity = new Vector2((Rx() - 0.5f) * 6f, -(20f + Rx() * 22f));
                    p.Sway = 4f + Rx() * 6f;
                    p.SwaySpeed = 2f + Rx() * 2f;
                    p.Width = p.Height = 2f + Rx() * 2f;
                    p.MaxLife = 1.1f + Rx() * 0.8f;
                    break;

                case ThemeEffectKind.Bubbles:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Bottom - 2);
                    p.Velocity = new Vector2((Rx() - 0.5f) * 4f, -(22f + Rx() * 16f));
                    p.Width = p.Height = 2f + Rx() * 3f;
                    p.MaxLife = b.Height / Math.Abs(p.Velocity.Y) + 0.25f;
                    break;

                case ThemeEffectKind.Snowfall:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Y - 6);
                    p.Velocity = new Vector2(0f, 10f + Rx() * 10f);
                    p.Sway = 6f + Rx() * 8f;
                    p.SwaySpeed = 1f + Rx();
                    p.Width = p.Height = 2f + Rx() * 2f;
                    p.MaxLife = b.Height / p.Velocity.Y + 0.5f;
                    break;

                case ThemeEffectKind.Sparkle:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Y + Rx() * b.Height);
                    p.Velocity = Vector2.Zero;
                    p.Width = p.Height = 2f + Rx() * 2f;
                    p.MaxLife = 0.4f + Rx() * 0.5f;
                    break;

                case ThemeEffectKind.NeonGlitch:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Y + Rx() * b.Height);
                    p.Velocity = Vector2.Zero;
                    p.Width = 8f + Rx() * 18f;
                    p.Height = 1f + Rx() * 1f;
                    p.MaxLife = 0.08f + Rx() * 0.15f;
                    break;

                case ThemeEffectKind.SandDrift:
                    p.Position = new Vector2(b.X - 6, b.Y + Rx() * b.Height);
                    p.Velocity = new Vector2(34f + Rx() * 24f, (Rx() - 0.5f) * 5f);
                    p.Width = 5f + Rx() * 6f;
                    p.Height = 1f + Rx() * 1f;
                    p.MaxLife = b.Width / p.Velocity.X + 0.25f;
                    break;

                case ThemeEffectKind.StaticNoise:
                    p.Position = new Vector2(b.X + Rx() * b.Width, b.Y + Rx() * b.Height);
                    p.Velocity = Vector2.Zero;
                    p.Width = p.Height = 1f;
                    p.MaxLife = 0.05f + Rx() * 0.08f;
                    break;
            }

            p.Seed = Rx() * 100f;
            return p;
        }

        private void AdvanceParticle(ref ThemeParticle p, ThemeEffectKind kind, float dt)
        {
            switch (kind)
            {
                case ThemeEffectKind.FallingLeaves:
                case ThemeEffectKind.Snowfall:
                    p.Position.X += (float)Math.Sin((p.Life + p.Seed) * p.SwaySpeed) * p.Sway * dt;
                    p.Position.Y += p.Velocity.Y * dt;
                    break;

                case ThemeEffectKind.Embers:
                    p.Position.X += (float)Math.Sin((p.Life + p.Seed) * p.SwaySpeed) * p.Sway * dt;
                    p.Position += p.Velocity * dt;
                    break;

                default:
                    p.Position += p.Velocity * dt;
                    break;
            }
        }

        private float GetFadeAlpha(ThemeEffectKind kind, float lifeRatio)
        {
            switch (kind)
            {
                case ThemeEffectKind.Sparkle:
                case ThemeEffectKind.NeonGlitch:
                case ThemeEffectKind.StaticNoise:
                    // kerlip cepat nyala-redup, bukan fade linear biasa
                    return (float)(0.35 + 0.65 * Math.Sin(lifeRatio * Math.PI));
                default:
                    float fadeIn = MathHelper.Clamp(lifeRatio / 0.15f, 0f, 1f);
                    float fadeOut = MathHelper.Clamp((1f - lifeRatio) / 0.25f, 0f, 1f);
                    return Math.Min(fadeIn, fadeOut);
            }
        }

        private Color GetParticleColor(ThemeEffectKind kind, DialogueTheme theme)
        {
            switch (kind)
            {
                case ThemeEffectKind.FallingLeaves: return Color.Lerp(new Color(90, 160, 60), theme.OutlineColor, 0.3f);
                case ThemeEffectKind.BloodDrip: return Color.Lerp(new Color(150, 6, 6), theme.OutlineColor, 0.2f);
                case ThemeEffectKind.Embers: return Color.Lerp(new Color(255, 140, 40), theme.NameTextColor, 0.3f);
                case ThemeEffectKind.Bubbles: return Color.Lerp(Color.White, theme.OutlineColor, 0.5f);
                case ThemeEffectKind.Snowfall: return Color.White;
                case ThemeEffectKind.Sparkle: return theme.NameTextColor;
                case ThemeEffectKind.NeonGlitch: return theme.OutlineColor;
                case ThemeEffectKind.SandDrift: return Color.Lerp(new Color(215, 185, 125), theme.OutlineColor, 0.3f);
                case ThemeEffectKind.StaticNoise: return theme.OutlineColor;
                default: return Color.White;
            }
        }
    }
}
