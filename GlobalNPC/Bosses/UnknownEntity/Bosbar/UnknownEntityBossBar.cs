using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.BigProgressBar;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity.Bosbar
{
    public class UnknownEntityBossBar : ModBossBar
    {
        private readonly List<BarGlowParticle> glowParticles = new List<BarGlowParticle>();
        private float animatedLifePercent = 1f;

        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
        {
            if (npc == null || drawParams.LifeMax <= 0)
                return true;

            float lifePercent = MathHelper.Clamp(drawParams.Life / drawParams.LifeMax, 0f, 1f);
            if (float.IsNaN(lifePercent)) lifePercent = 0f;

            animatedLifePercent = MathHelper.Lerp(animatedLifePercent, lifePercent, 0.08f);

            // ==================== 1. DIMENSI & TATA LETAK ====================
            Vector2 center = drawParams.BarCenter;
            int barWidth = 400;
            int barHeight = 24;
            int iconSize = 40;
            int gap = 12;

            int totalWidth = barWidth + iconSize + gap;
            int startX = (int)center.X - totalWidth / 2;
            int startY = (int)center.Y - barHeight / 2;

            Rectangle iconBox = new Rectangle(startX, (int)center.Y - iconSize / 2, iconSize, iconSize);
            Rectangle barBox = new Rectangle(startX + iconSize + gap, startY, barWidth, barHeight);

            // Inset Box khusus HP Fill (Padding 3px agar border terpisah rapi)
            int padding = 3;
            Rectangle hpBox = new Rectangle(barBox.X + padding, barBox.Y + padding, barBox.Width - (padding * 2), barBox.Height - (padding * 2));

            int currentHpW = (int)(hpBox.Width * lifePercent);
            int damageHpW = (int)(hpBox.Width * animatedLifePercent);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D softGlow = TextureAssets.Extra[88].Value;
            Texture2D starFlare = TextureAssets.Extra[98].Value;
            Texture2D noiseTex = TextureAssets.Extra[193].Value; // Texture Noise Cosmic

            float time = (float)Main.GlobalTimeWrappedHourly;
            float pulseSpeed = MathHelper.Lerp(10f, 4f, lifePercent);
            float pulse = (float)Math.Sin(time * pulseSpeed) * 0.15f + 0.85f;
            float fastPulse = (float)Math.Sin(time * (pulseSpeed * 2f)) * 0.25f + 0.75f;

            // ==================== 2. BACKGROUND PANELS ====================
            Color bgDark = new Color(6, 3, 14) * 0.98f;
            spriteBatch.Draw(pixel, iconBox, bgDark);
            spriteBatch.Draw(pixel, barBox, bgDark);

            // ==================== 3. CHIP DAMAGE TRAIL ====================
            if (damageHpW > currentHpW)
            {
                Rectangle damageRect = new Rectangle(hpBox.X + currentHpW, hpBox.Y, damageHpW - currentHpW, hpBox.Height);
                Color damageColor = Color.Lerp(new Color(230, 40, 90), new Color(255, 120, 0), (float)Math.Sin(time * 6f) * 0.5f + 0.5f);
                spriteBatch.Draw(pixel, damageRect, damageColor * 0.85f);
            }

            // ==================== 4. HP BAR DYNAMIC FILL ====================
            if (currentHpW > 0)
            {
                // Gradient Dasar HP
                for (int x = 0; x < currentHpW; x++)
                {
                    float progress = x / (float)hpBox.Width;
                    Color col = GetDynamicCosmicColor(progress, time, lifePercent);
                    spriteBatch.Draw(pixel, new Rectangle(hpBox.X + x, hpBox.Y, 1, hpBox.Height), col);
                }

                // ==================== 4.1 HIGH-INTENSITY DUAL NOISE OVERLAY ====================
                if (noiseTex != null)
                {
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

                    float noiseSpeed = 110f;
                    float density = 2.4f; // Kepadatan grain noise (makin tinggi makin tajam/rapat)

                    Rectangle noiseDest = new Rectangle(hpBox.X, hpBox.Y, currentHpW, hpBox.Height);

                    // --- LAYER 1: Main Forward Noise (Kiri ke Kanan) ---
                    int scrollX1 = (int)(time * noiseSpeed);
                    int scrollY1 = (int)(time * (noiseSpeed * 0.4f));
                    Rectangle source1 = new Rectangle(scrollX1, scrollY1, (int)(currentHpW * density), (int)(hpBox.Height * density));

                    float mainNoiseAlpha = MathHelper.Lerp(0.70f, 0.95f, 1f - lifePercent);
                    Color noiseColor1 = Color.Lerp(new Color(220, 130, 255), new Color(255, 70, 110), 1f - lifePercent) * mainNoiseAlpha * pulse;

                    spriteBatch.Draw(noiseTex, noiseDest, source1, noiseColor1);

                    // --- LAYER 2: Counter Turbulence Noise (Kanan ke Kiri untuk gejolak energi) ---
                    int scrollX2 = (int)(-time * (noiseSpeed * 1.4f));
                    int scrollY2 = (int)(time * (noiseSpeed * 0.7f));
                    Rectangle source2 = new Rectangle(scrollX2, scrollY2, (int)(currentHpW * (density * 1.3f)), (int)(hpBox.Height * (density * 1.3f)));

                    Color noiseColor2 = Color.Cyan * 0.50f * fastPulse;

                    spriteBatch.Draw(noiseTex, noiseDest, source2, noiseColor2);

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
                }

                // Shimmers
                float shimmer1Prog = (float)((time * 1.5f) % 1.0f);
                int shimmer1X = hpBox.X + (int)(shimmer1Prog * (hpBox.Width + 50)) - 25;
                DrawShimmerLine(spriteBatch, pixel, hpBox, currentHpW, shimmer1X, 20, Color.Cyan * 0.35f);

                float shimmer2Prog = (float)((time * 0.7f + 0.5f) % 1.0f);
                int shimmer2X = hpBox.X + (int)(shimmer2Prog * (hpBox.Width + 70)) - 35;
                Color shimmer2Color = lifePercent < 0.35f ? Color.Gold * 0.4f : Color.White * 0.25f;
                DrawShimmerLine(spriteBatch, pixel, hpBox, currentHpW, shimmer2X, 12, shimmer2Color);

                // Gloss Kaca 3D
                int glossH = (int)(hpBox.Height * 0.45f);
                spriteBatch.Draw(pixel, new Rectangle(hpBox.X, hpBox.Y, currentHpW, glossH), Color.White * 0.15f);
            }

            // ==================== 5. IKON HEAD BOSS ====================
            int headIndex = npc.GetBossHeadTextureIndex();
            if (headIndex >= 0 && headIndex < TextureAssets.NpcHeadBoss.Length)
            {
                Texture2D headTex = TextureAssets.NpcHeadBoss[headIndex].Value;
                if (headTex != null)
                {
                    Vector2 origin = headTex.Size() * 0.5f;
                    Vector2 iconCenter = iconBox.Center.ToVector2();
                    float scale = Math.Min((float)(iconSize - 8) / headTex.Width, (float)(iconSize - 8) / headTex.Height);
                    spriteBatch.Draw(headTex, iconCenter, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
                }
            }

            // ==================== 6. PARTIKEL GLOW LOKAL ====================
            int particleSpawnRate = lifePercent < 0.3f ? 1 : (lifePercent < 0.6f ? 2 : 3);
            if (currentHpW > 0 && Main.rand.NextBool(particleSpawnRate))
            {
                float pSpeedY = MathHelper.Lerp(-2.0f, -0.6f, lifePercent);
                Color pColor = GetParticleColor(time, lifePercent);

                glowParticles.Add(new BarGlowParticle(
                    new Vector2(hpBox.X + Main.rand.NextFloat(0, currentHpW), hpBox.Bottom - 1),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(pSpeedY, -0.4f)),
                    pColor,
                    Main.rand.NextFloat(18f, 36f)
                ));
            }

            // Render Partikel Additive
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            Vector2 glowOrigin = softGlow.Size() * 0.5f;
            for (int i = glowParticles.Count - 1; i >= 0; i--)
            {
                var p = glowParticles[i];
                p.Update();

                if (!p.Active)
                {
                    glowParticles.RemoveAt(i);
                    continue;
                }

                if (p.Position.X >= hpBox.X && p.Position.X <= hpBox.Right)
                {
                    spriteBatch.Draw(softGlow, p.Position, null, p.ParticleColor * p.Alpha * 0.65f, 0f, glowOrigin, p.CurrentScale * 0.15f, SpriteEffects.None, 0f);
                }
            }

            if (currentHpW <= 0) glowParticles.Clear();

            // ==================== 7. AMBIENT BORDER BLOOM ====================
            DrawDynamicAmbientGlow(spriteBatch, softGlow, barBox, iconBox, time, pulse, lifePercent);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            // ==================== 8. CRISP SCI-FI BORDER ====================
            DrawCrispSciFiBorder(spriteBatch, pixel, barBox, iconBox, time, pulse, lifePercent);

            // ==================== 9. TEKS NAMA & HP ====================
            string bossName = npc.GivenOrTypeName;
            string hpText = $"{drawParams.Life} / {drawParams.LifeMax} ({(lifePercent * 100f):F1}%)";

            Vector2 nameSize = FontAssets.MouseText.Value.MeasureString(bossName);
            Vector2 namePos = new Vector2(barBox.Center.X - nameSize.X * 0.5f, barBox.Y - 23f);

            // Soft Glow Pendaran di Belakang Nama Boss
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
            spriteBatch.Draw(softGlow, namePos + nameSize * 0.5f, null, new Color(180, 90, 255) * 0.25f * pulse, 0f, glowOrigin, new Vector2(1.8f, 0.35f), SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            Color nameTextColor = Color.Lerp(new Color(230, 200, 255), new Color(255, 180, 190), (1f - lifePercent));
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, bossName, namePos, nameTextColor, 0f, Vector2.Zero, Vector2.One);

            Vector2 hpSize = FontAssets.MouseText.Value.MeasureString(hpText) * 0.85f;
            Vector2 hpPos = new Vector2(barBox.Center.X - hpSize.X * 0.5f, barBox.Center.Y - hpSize.Y * 0.5f);
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, hpText, hpPos, Color.White, 0f, Vector2.Zero, new Vector2(0.85f));

            // ==================== 10. SHINE FLARES ====================
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            DrawDynamicShineFlares(spriteBatch, starFlare, barBox, iconBox, bossName, namePos, time, pulse, fastPulse, lifePercent);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            return false;
        }

        // ==================== BORDER & SHINE FLARE DRAWING ====================

        private void DrawDynamicShineFlares(SpriteBatch sb, Texture2D starFlare, Rectangle barBox, Rectangle iconBox, string bossName, Vector2 namePos, float time, float pulse, float fastPulse, float hpPercent)
        {
            Vector2 starOrigin = starFlare.Size() * 0.5f;

            Color primaryFlare = Color.Lerp(new Color(120, 230, 255), new Color(255, 90, 130), (1f - hpPercent)) * fastPulse;
            Color secondaryFlare = new Color(220, 140, 255) * pulse;

            float rot1 = time * 3.0f;
            float rot2 = -time * 2.2f;

            // 1. Shine Flare Kiri Atas Border
            Vector2 barTopLeft = new Vector2(barBox.X - 1, barBox.Y - 1);
            sb.Draw(starFlare, barTopLeft, null, primaryFlare * 0.95f, rot1, starOrigin, 0.24f * fastPulse, SpriteEffects.None, 0f);
            sb.Draw(starFlare, barTopLeft, null, Color.White * 0.85f, rot2, starOrigin, 0.14f, SpriteEffects.None, 0f);

            // 2. Shine Flare Sudut Border Icon Boss (Ukuran 0.35f)
            float iconFlareScale = 0.35f * fastPulse;
            Vector2 iconTopLeft = new Vector2(iconBox.X - 1, iconBox.Y - 1);
            Vector2 iconTopRight = new Vector2(iconBox.Right + 1, iconBox.Y - 1);
            Vector2 iconBottomLeft = new Vector2(iconBox.X - 1, iconBox.Bottom + 1);
            Vector2 iconBottomRight = new Vector2(iconBox.Right + 1, iconBox.Bottom + 1);

            sb.Draw(starFlare, iconTopLeft, null, primaryFlare, rot1, starOrigin, iconFlareScale, SpriteEffects.None, 0f);
            sb.Draw(starFlare, iconTopLeft, null, Color.White * 0.9f, rot2, starOrigin, iconFlareScale * 0.55f, SpriteEffects.None, 0f);

            sb.Draw(starFlare, iconBottomRight, null, primaryFlare, rot2, starOrigin, iconFlareScale, SpriteEffects.None, 0f);
            sb.Draw(starFlare, iconBottomRight, null, Color.White * 0.9f, rot1, starOrigin, iconFlareScale * 0.55f, SpriteEffects.None, 0f);

            sb.Draw(starFlare, iconTopRight, null, secondaryFlare * 0.9f, rot2 * 1.2f, starOrigin, iconFlareScale * 0.85f, SpriteEffects.None, 0f);
            sb.Draw(starFlare, iconBottomLeft, null, secondaryFlare * 0.9f, rot1 * 1.2f, starOrigin, iconFlareScale * 0.85f, SpriteEffects.None, 0f);

            // 3. Shine Flare Huruf 'o' Nama Boss
            int oIndex = bossName.IndexOf('o');
            if (oIndex < 0) oIndex = bossName.IndexOf('O');

            Vector2 nameFlarePos = namePos;
            if (oIndex >= 0)
            {
                string prefix = bossName.Substring(0, oIndex);
                float prefixWidth = FontAssets.MouseText.Value.MeasureString(prefix).X;
                float oWidth = FontAssets.MouseText.Value.MeasureString(bossName[oIndex].ToString()).X;
                float nameHeight = FontAssets.MouseText.Value.MeasureString(bossName).Y;

                nameFlarePos = namePos + new Vector2(prefixWidth + oWidth * 0.5f, nameHeight * 0.42f);
            }
            else
            {
                nameFlarePos = namePos + FontAssets.MouseText.Value.MeasureString(bossName) * 0.5f;
            }

            sb.Draw(starFlare, nameFlarePos, null, new Color(255, 200, 255) * fastPulse, rot1 * 1.2f, starOrigin, 0.20f * pulse, SpriteEffects.None, 0f);
            sb.Draw(starFlare, nameFlarePos, null, Color.Cyan * 0.7f, rot2 * 1.5f, starOrigin, 0.12f, SpriteEffects.None, 0f);

            // 4. Shine Flares Tambahan (Sayap & Kanan Bar)
            Vector2 wingTipPos = new Vector2(barBox.Right + 8, barBox.Center.Y);
            sb.Draw(starFlare, wingTipPos, null, primaryFlare, rot1, starOrigin, 0.28f * fastPulse, SpriteEffects.None, 0f);
            sb.Draw(starFlare, wingTipPos, null, Color.White * 0.85f, rot2, starOrigin, 0.16f, SpriteEffects.None, 0f);

            Vector2 barBottomRight = new Vector2(barBox.Right + 1, barBox.Bottom + 1);
            sb.Draw(starFlare, barBottomRight, null, primaryFlare * 0.8f, rot2, starOrigin, 0.20f, SpriteEffects.None, 0f);

            // 5. Dynamic Traveling Flare
            float travelProg = (time * 0.9f) % 1.0f;
            Vector2 travelPos = travelProg < 0.5f
                ? new Vector2(MathHelper.Lerp(barBox.X, barBox.Right, travelProg / 0.5f), barBox.Y)
                : new Vector2(MathHelper.Lerp(barBox.Right, barBox.X, (travelProg - 0.5f) / 0.5f), barBox.Bottom);

            sb.Draw(starFlare, travelPos, null, secondaryFlare * 0.9f, rot1 * 2f, starOrigin, 0.16f, SpriteEffects.None, 0f);
        }

        private void DrawCrispSciFiBorder(SpriteBatch sb, Texture2D pixel, Rectangle barBox, Rectangle iconBox, float time, float pulse, float hpPercent)
        {
            Color pureBlackShadow = new Color(2, 1, 5);

            float borderHue = (float)Math.Sin(time * 2.5f) * 0.5f + 0.5f;
            Color mainNeon = Color.Lerp(new Color(185, 120, 255), new Color(60, 225, 255), borderHue) * pulse;
            Color accentColor = Color.Lerp(new Color(60, 225, 255), new Color(255, 80, 140), (1f - hpPercent));
            Color gemColor = Color.Lerp(new Color(240, 180, 255), new Color(255, 220, 100), borderHue);

            // Connector
            int connY = barBox.Center.Y - 1;
            int connW = barBox.X - iconBox.Right;
            sb.Draw(pixel, new Rectangle(iconBox.Right, connY - 1, connW, 4), pureBlackShadow);
            sb.Draw(pixel, new Rectangle(iconBox.Right, connY, connW, 2), mainNeon);

            // Double Stroke Shadow
            DrawOutline(sb, pixel, Inflate(barBox, 1), pureBlackShadow, 2);
            DrawOutline(sb, pixel, Inflate(iconBox, 1), pureBlackShadow, 2);
            DrawOutline(sb, pixel, Inflate(barBox, -1), pureBlackShadow, 1);
            DrawOutline(sb, pixel, Inflate(iconBox, -1), pureBlackShadow, 1);

            // Outlines Utama
            DrawOutline(sb, pixel, barBox, mainNeon, 1);
            DrawOutline(sb, pixel, iconBox, mainNeon, 1);

            // Sci-Fi Corner Brackets
            DrawCorners(sb, pixel, barBox, 10, 2, accentColor);
            DrawCorners(sb, pixel, iconBox, 10, 2, accentColor);

            // Side Wings
            int wingH = 12;
            int wingY = barBox.Center.Y - wingH / 2;
            sb.Draw(pixel, new Rectangle(barBox.Right + 3, wingY - 1, 3, wingH + 2), pureBlackShadow);
            sb.Draw(pixel, new Rectangle(barBox.Right + 3, wingY, 3, wingH), mainNeon);

            sb.Draw(pixel, new Rectangle(barBox.Right + 7, wingY + 1, 2, wingH - 2), pureBlackShadow);
            sb.Draw(pixel, new Rectangle(barBox.Right + 7, wingY + 2, 2, wingH - 4), accentColor);

            // Gem Nodes
            DrawGemNode(sb, pixel, iconBox.X - 1, iconBox.Y - 1, gemColor);
            DrawGemNode(sb, pixel, iconBox.Right + 1, iconBox.Y - 1, gemColor);
            DrawGemNode(sb, pixel, iconBox.X - 1, iconBox.Bottom + 1, gemColor);
            DrawGemNode(sb, pixel, iconBox.Right + 1, iconBox.Bottom + 1, gemColor);
        }

        private Color GetDynamicCosmicColor(float progress, float time, float hpPercent)
        {
            float wave1 = (float)Math.Sin(time * 2.5f + progress * 7f) * 0.5f + 0.5f;
            float wave2 = (float)Math.Cos(time * 1.8f - progress * 5f) * 0.5f + 0.5f;

            Color highHpPrimary = Color.Lerp(new Color(125, 20, 220), new Color(20, 110, 245), wave1);
            Color highHpSecondary = Color.Lerp(new Color(0, 210, 255), new Color(230, 50, 180), wave2);
            Color baseHighHp = Color.Lerp(highHpPrimary, highHpSecondary, wave1 * 0.5f + 0.25f);

            Color lowHpPrimary = Color.Lerp(new Color(255, 30, 70), new Color(160, 10, 120), wave1);
            Color lowHpSecondary = Color.Lerp(new Color(255, 140, 0), new Color(220, 0, 100), wave2);
            Color baseLowHp = Color.Lerp(lowHpPrimary, lowHpSecondary, wave2);

            float dangerFactor = MathHelper.Clamp((1f - hpPercent) * 1.6f, 0f, 1f);
            return Color.Lerp(baseHighHp, baseLowHp, dangerFactor);
        }

        private Color GetParticleColor(float time, float hpPercent)
        {
            float t = (float)Math.Sin(time * 4f) * 0.5f + 0.5f;
            if (hpPercent < 0.35f)
            {
                return Color.Lerp(new Color(255, 60, 90), new Color(255, 180, 40), t);
            }
            return Color.Lerp(new Color(210, 110, 255), new Color(70, 210, 255), t);
        }

        private void DrawShimmerLine(SpriteBatch sb, Texture2D pixel, Rectangle hpBox, int currentHpW, int shimmerX, int shimmerW, Color color)
        {
            int drawX1 = Math.Max(hpBox.X, shimmerX);
            int drawX2 = Math.Min(hpBox.X + currentHpW, shimmerX + shimmerW);
            if (drawX2 > drawX1)
            {
                Rectangle shimmerRect = new Rectangle(drawX1, hpBox.Y, drawX2 - drawX1, hpBox.Height);
                sb.Draw(pixel, shimmerRect, color);
            }
        }

        private void DrawDynamicAmbientGlow(SpriteBatch sb, Texture2D softGlow, Rectangle barBox, Rectangle iconBox, float time, float pulse, float hpPercent)
        {
            Vector2 origin = softGlow.Size() * 0.5f;
            float hueShift = (float)Math.Sin(time * 3f) * 0.5f + 0.5f;

            Color cyanGlow = Color.Lerp(new Color(50, 200, 255), new Color(255, 60, 120), (1f - hpPercent));
            Color purpleGlow = Color.Lerp(new Color(180, 90, 255), new Color(255, 160, 30), hueShift) * 0.35f * pulse;

            sb.Draw(softGlow, new Vector2(iconBox.X, iconBox.Y), null, cyanGlow * 0.35f * pulse, 0f, origin, 0.32f, SpriteEffects.None, 0f);
            sb.Draw(softGlow, new Vector2(iconBox.Right, iconBox.Y), null, cyanGlow * 0.35f * pulse, 0f, origin, 0.32f, SpriteEffects.None, 0f);
            sb.Draw(softGlow, new Vector2(iconBox.X, iconBox.Bottom), null, cyanGlow * 0.35f * pulse, 0f, origin, 0.32f, SpriteEffects.None, 0f);
            sb.Draw(softGlow, new Vector2(iconBox.Right, iconBox.Bottom), null, cyanGlow * 0.35f * pulse, 0f, origin, 0.32f, SpriteEffects.None, 0f);

            Vector2 barCenterTop = new Vector2(barBox.Center.X, barBox.Y);
            Vector2 barCenterBottom = new Vector2(barBox.Center.X, barBox.Bottom);
            sb.Draw(softGlow, barCenterTop, null, purpleGlow, 0f, origin, new Vector2(3.0f, 0.14f), SpriteEffects.None, 0f);
            sb.Draw(softGlow, barCenterBottom, null, purpleGlow, 0f, origin, new Vector2(3.0f, 0.14f), SpriteEffects.None, 0f);
        }

        private void DrawGemNode(SpriteBatch sb, Texture2D pixel, int x, int y, Color color)
        {
            sb.Draw(pixel, new Rectangle(x - 1, y - 1, 3, 3), color);
        }

        private void DrawOutline(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int t)
        {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), color);
        }

        private void DrawCorners(SpriteBatch sb, Texture2D pixel, Rectangle r, int len, int t, Color color)
        {
            sb.Draw(pixel, new Rectangle(r.X - 2, r.Y - 2, len, t), color);
            sb.Draw(pixel, new Rectangle(r.X - 2, r.Y - 2, t, len), color);
            sb.Draw(pixel, new Rectangle(r.Right + 2 - len, r.Y - 2, len, t), color);
            sb.Draw(pixel, new Rectangle(r.Right + 2 - t, r.Y - 2, t, len), color);
            sb.Draw(pixel, new Rectangle(r.X - 2, r.Bottom + 2 - t, len, t), color);
            sb.Draw(pixel, new Rectangle(r.X - 2, r.Bottom + 2 - len, t, len), color);
            sb.Draw(pixel, new Rectangle(r.Right + 2 - len, r.Bottom + 2 - t, len, t), color);
            sb.Draw(pixel, new Rectangle(r.Right + 2 - t, r.Bottom + 2 - len, t, len), color);
        }

        private Rectangle Inflate(Rectangle r, int val) => new Rectangle(r.X - val, r.Y - val, r.Width + val * 2, r.Height + val * 2);
    }

    public class BarGlowParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color ParticleColor;
        public float CurrentScale;
        public float Alpha;
        public float Lifetime;
        public float MaxLifetime;

        public bool Active => Lifetime < MaxLifetime;

        public BarGlowParticle(Vector2 position, Vector2 velocity, Color color, float maxLifetime)
        {
            Position = position;
            Velocity = velocity;
            ParticleColor = color;
            CurrentScale = 1f;
            MaxLifetime = maxLifetime;
            Lifetime = 0f;
            Alpha = 1f;
        }

        public void Update()
        {
            Lifetime++;
            Position += Velocity;
            float progress = Lifetime / MaxLifetime;
            CurrentScale = 1f - progress;
            Alpha = 1f - progress;
        }
    }
}