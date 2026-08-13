using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public partial class UnknownEntity
    {
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (headTexture?.Value == null || bodyTexture?.Value == null || legTexture?.Value == null)
                return false;

            Texture2D headTex = headTexture.Value;
            Texture2D bodyTex = bodyTexture.Value;
            Texture2D legTex = legTexture.Value;

            Color alphaDrawColor = NPC.GetAlpha(drawColor);
            SpriteEffects spriteEffects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Offset posisi Boss saat menunggangi Bone Serpent
            Vector2 ridingOffset = (State == AIState.SerpentPortalDash) ? new Vector2(0f, -32f).RotatedBy(NPC.rotation) : Vector2.Zero;
            Vector2 drawPos = NPC.Center + ridingOffset - screenPos + new Vector2(0f, NPC.gfxOffY + 4f);

            // ==================== -1. CUTSCENE: INTRO REALITY TEAR ====================
            if (State == AIState.Awakening)
            {
                float openness = AwakeningPortalOpenness();
                float huePulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
                Color tearColor = Color.Lerp(Color.Cyan, Color.Magenta, huePulse);

                DrawRealityTear(spriteBatch, screenPos, NPC.Center, openness, tearColor);
            }

            // ==================== -1. CUTSCENE: DEATH SEQUENCE (TYPING + PORTAL EXIT) ====================
            if (State == AIState.DeathSequence)
            {
                float openness = DeathPortalOpenness();
                if (openness > 0f)
                {
                    float huePulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
                    Color tearColor = isPhase2 ? Color.Lerp(Color.Crimson, Color.Purple, huePulse) : Color.Lerp(Color.Cyan, Color.Magenta, huePulse);
                    DrawRealityTear(spriteBatch, screenPos, NPC.Center, openness, tearColor);
                }

                DrawDeathTypingText(spriteBatch, screenPos);
            }

            // ==================== 0. TEMPORARY BONE SERPENT MOUNT WITH SHADER ====================
            if (State == AIState.SerpentPortalDash)
            {
                Texture2D serpentHead = TextureAssets.Npc[NPCID.BoneSerpentHead].Value;
                Texture2D serpentBody = TextureAssets.Npc[NPCID.BoneSerpentBody].Value;
                Texture2D serpentTail = TextureAssets.Npc[NPCID.BoneSerpentTail].Value;

                float serpentScale = 1.6f;
                int totalSegments = 14;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                // Mengaplikasikan Shader (ShadowDye) pada Bone Serpent
                int shaderId = GameShaders.Armor.GetShaderIdFromItemId(ItemID.ShadowDye);
                GameShaders.Armor.GetSecondaryShader(shaderId, Main.LocalPlayer).Apply(null);

                Vector2 previousPos = NPC.Center;

                // Render Segmen Tubuh (Body) & Ekor (Tail)
                for (int i = 1; i <= totalSegments; i++)
                {
                    int oldPosIndex = Math.Min(i * 2, NPC.oldPos.Length - 1);
                    Vector2 segmentPos = (NPC.oldPos[oldPosIndex] != Vector2.Zero ? NPC.oldPos[oldPosIndex] + NPC.Size * 0.5f : NPC.Center) - screenPos;

                    Vector2 dirToPrev = (previousPos - segmentPos).SafeNormalize(Vector2.UnitX);
                    float segmentRot = dirToPrev.ToRotation() + MathHelper.PiOver2;

                    Texture2D currentTex = (i == totalSegments) ? serpentTail : serpentBody;
                    Vector2 origin = currentTex.Size() * 0.5f;

                    Color segmentColor = Color.Lerp(Color.Purple, Color.Cyan, i / (float)totalSegments);

                    spriteBatch.Draw(currentTex, segmentPos, null, segmentColor, segmentRot, origin, serpentScale, SpriteEffects.None, 0f);
                    previousPos = segmentPos;
                }

                // Render Kepala Serpent (Head)
                Vector2 headDrawPos = NPC.Center - screenPos;
                float headRot = NPC.rotation + MathHelper.PiOver2;
                Vector2 headOrigin = serpentHead.Size() * 0.5f;

                spriteBatch.Draw(serpentHead, headDrawPos, null, Color.Magenta, headRot, headOrigin, serpentScale, SpriteEffects.None, 0f);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // ==================== 1. PRISM MIRAGE CLONES & CROSS TELEGRAPH ====================
            if (State == AIState.PrismMirage)
            {
                float baseAngle = dashTargetDir.ToRotation();
                float time = (float)Main.GlobalTimeWrappedHourly;

                if (StateTimer < 40 && telegraphTexture?.Value != null)
                {
                    Texture2D telegraphTex = telegraphTexture.Value;
                    float alpha = (StateTimer / 40f) * ((255 - NPC.alpha) / 255f);
                    float pulse = 1f + (float)Math.Sin(time * 30f) * 0.35f;

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                    for (int i = 0; i < 2; i++)
                    {
                        float lineAngle = baseAngle + (i * MathHelper.PiOver2);
                        Vector2 lineStartPos = startPos - lineAngle.ToRotationVector2() * 700f - screenPos;
                        Vector2 origin = new Vector2(0f, telegraphTex.Height / 2f);
                        Vector2 scale = new Vector2(1400f / telegraphTex.Width, pulse * 1.2f);

                        Color telegraphColor = Color.Lerp(Color.Magenta, Color.Cyan, (float)Math.Sin(time * 10f + i) * 0.5f + 0.5f) * alpha * 0.85f;
                        spriteBatch.Draw(telegraphTex, lineStartPos, null, telegraphColor, lineAngle, origin, scale, SpriteEffects.None, 0f);
                    }

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }

                for (int i = 1; i < 4; i++)
                {
                    float angle = baseAngle + (i * MathHelper.PiOver2);
                    Vector2 cloneStart = startPos + angle.ToRotationVector2() * 600f;
                    Vector2 cloneCurrentPos = cloneStart;

                    if (StateTimer >= 40)
                    {
                        Vector2 cloneDashDir = (startPos - cloneStart).SafeNormalize(Vector2.UnitX);
                        cloneCurrentPos = cloneStart + (cloneDashDir * (52f * (StateTimer - 40)));
                    }

                    Vector2 cloneDrawPos = cloneCurrentPos - screenPos + new Vector2(0f, NPC.gfxOffY + 4f);
                    float cloneRotation = (startPos - cloneStart).ToRotation();

                    Color cloneColor = Color.Lerp(Color.DeepPink, Color.Cyan, i / 3f) * 0.75f;

                    int frameH = 56;
                    int frameW = 40;
                    Rectangle headSrc = new Rectangle(0, (frameIndex % 20) * frameH, headTex.Width, frameH);
                    Rectangle bodySrc = new Rectangle(0, 0, frameW, frameH);

                    Vector2 headOrig = new Vector2(headTex.Width / 2f, frameH / 2f);
                    Vector2 bodyOrig = new Vector2(frameW / 2f, frameH / 2f);

                    spriteBatch.Draw(legTex, cloneDrawPos, headSrc, cloneColor, cloneRotation, headOrig, NPC.scale, spriteEffects, 0f);
                    spriteBatch.Draw(bodyTex, cloneDrawPos, bodySrc, cloneColor, cloneRotation, bodyOrig, NPC.scale, spriteEffects, 0f);
                    spriteBatch.Draw(headTex, cloneDrawPos, headSrc, cloneColor, cloneRotation, headOrig, NPC.scale, spriteEffects, 0f);
                }
            }

            // ==================== 2. RITUAL SPELL SEAL ====================
            if (State == AIState.ProjectileRing && shineFlareTex?.Value != null && bloomCircleTex?.Value != null)
            {
                Texture2D flare = shineFlareTex.Value;
                Texture2D circle = bloomCircleTex.Value;
                float time = (float)Main.GlobalTimeWrappedHourly;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                if (StateTimer < 35)
                {
                    float chargeProgress = StateTimer / 35f;
                    Color chargeColor = Color.Lerp(Color.DeepPink, Color.Cyan, chargeProgress) * chargeProgress;

                    Vector2 circleOrigin = circle.Size() * 0.5f;
                    Vector2 flareOrigin = flare.Size() * 0.5f;

                    spriteBatch.Draw(circle, drawPos, null, chargeColor, 0f, circleOrigin, 2.5f * chargeProgress, SpriteEffects.None, 0f);
                    spriteBatch.Draw(flare, drawPos, null, Color.White * chargeProgress, time * 6f, flareOrigin, 1.8f * chargeProgress, SpriteEffects.None, 0f);
                }
                else if (StateTimer >= 35 && StateTimer <= 105)
                {
                    float pulse = 1f + (float)Math.Sin(time * 20f) * 0.2f;
                    Color haloColor = Color.Lerp(Color.Cyan, Color.Magenta, (float)Math.Sin(time * 4f) * 0.5f + 0.5f) * 0.85f;

                    Vector2 circleOrigin = circle.Size() * 0.5f;
                    Vector2 flareOrigin = flare.Size() * 0.5f;

                    spriteBatch.Draw(circle, drawPos, null, haloColor, 0f, circleOrigin, 3f * pulse, SpriteEffects.None, 0f);
                    spriteBatch.Draw(flare, drawPos, null, Color.Gold * 0.7f, time * 3f, flareOrigin, 2.2f * pulse, SpriteEffects.None, 0f);
                    spriteBatch.Draw(flare, drawPos, null, haloColor, -time * 4f, flareOrigin, 2.6f * pulse, SpriteEffects.None, 0f);
                }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // ==================== 3. PORTAL BLOOM ====================
            if ((State == AIState.WormholeDash || State == AIState.SerpentPortalDash) && shineFlareTex?.Value != null && bloomCircleTex?.Value != null)
            {
                Texture2D flare = shineFlareTex.Value;
                Texture2D circle = bloomCircleTex.Value;

                Vector2 portalDrawPos = portalExitPos - screenPos;
                float time = (float)Main.GlobalTimeWrappedHourly;
                float pulse = 1f + (float)Math.Sin(time * 22f) * 0.25f;
                float visibility = (255 - NPC.alpha) / 255f;

                Color outerColor = Color.Lerp(Color.DeepPink, Color.BlueViolet, (float)Math.Sin(time * 3f) * 0.5f + 0.5f) * visibility * 0.85f;
                Color innerColor = Color.Lerp(Color.Cyan, Color.Gold, (float)Math.Cos(time * 5f) * 0.5f + 0.5f) * visibility * 0.95f;

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                Vector2 circleOrigin = circle.Size() * 0.5f;
                spriteBatch.Draw(circle, portalDrawPos, null, outerColor, 0f, circleOrigin, 2.4f * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(circle, portalDrawPos, null, innerColor, 0f, circleOrigin, 1.2f * pulse, SpriteEffects.None, 0f);

                Vector2 flareOrigin = flare.Size() * 0.5f;
                spriteBatch.Draw(flare, portalDrawPos, null, innerColor * 0.8f, time * 4f, flareOrigin, 1.4f * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(flare, portalDrawPos, null, outerColor * 0.9f, -time * 3f, flareOrigin, 1.8f * pulse, SpriteEffects.None, 0f);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // ==================== 4. PHANTOM GRID SLASHES ====================
            if (State == AIState.PhantomGrid)
            {
                float time = (float)Main.GlobalTimeWrappedHourly;
                Vector2 centerDrawPos = startPos - screenPos;

                int windupDuration = 55;
                int dashDuration = 20;

                int wingSlotClones = ContentSamples.ItemsByType[ItemID.SpookyWings].wingSlot;
                Main.instance.LoadWings(wingSlotClones);
                Texture2D cloneWingTex = TextureAssets.Wings[wingSlotClones]?.Value;

                if (StateTimer < windupDuration && telegraphTexture?.Value != null)
                {
                    Texture2D telegraphTex = telegraphTexture.Value;
                    float alpha = (StateTimer / (float)windupDuration);
                    float pulse = 1.2f + (float)Math.Sin(time * 30f) * 0.35f;

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                    for (int i = 0; i < 2; i++)
                    {
                        float lineAngle = MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                        Vector2 lineStart = centerDrawPos - lineAngle.ToRotationVector2() * 900f;
                        Vector2 origin = new Vector2(0f, telegraphTex.Height / 2f);

                        Color outerCol = Color.Lerp(Color.DeepPink, Color.Magenta, (float)Math.Sin(time * 12f + i) * 0.5f + 0.5f) * alpha * 0.75f;
                        Vector2 outerScale = new Vector2(1800f / telegraphTex.Width, pulse * 2.2f);
                        spriteBatch.Draw(telegraphTex, lineStart, null, outerCol, lineAngle, origin, outerScale, SpriteEffects.None, 0f);

                        Color coreCol = Color.Cyan * alpha * 0.9f;
                        Vector2 coreScale = new Vector2(1800f / telegraphTex.Width, pulse * 0.8f);
                        spriteBatch.Draw(telegraphTex, lineStart, null, coreCol, lineAngle, origin, coreScale, SpriteEffects.None, 0f);
                    }

                    if (bloomCircleTex?.Value != null && shineFlareTex?.Value != null)
                    {
                        Texture2D circle = bloomCircleTex.Value;
                        Texture2D flare = shineFlareTex.Value;

                        Vector2 circleOrig = circle.Size() * 0.5f;
                        Vector2 flareOrig = flare.Size() * 0.5f;

                        Color coreCol = Color.Lerp(Color.DeepPink, Color.Cyan, alpha) * alpha;
                        spriteBatch.Draw(circle, centerDrawPos, null, coreCol, 0f, circleOrig, 4f * alpha, SpriteEffects.None, 0f);

                        spriteBatch.Draw(flare, centerDrawPos, null, Color.Cyan * alpha, time * 10f, flareOrig, 2.8f * alpha, SpriteEffects.None, 0f);
                        spriteBatch.Draw(flare, centerDrawPos, null, Color.White * alpha, -time * 8f, flareOrig, 1.8f * alpha, SpriteEffects.None, 0f);
                    }

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }

                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                    Vector2 slashDir = angle.ToRotationVector2();

                    Vector2 cloneStart = centerDrawPos - slashDir * 750f;
                    Vector2 cloneEnd = centerDrawPos + slashDir * 750f;
                    Vector2 currentClonePos = cloneStart;

                    if (StateTimer < windupDuration)
                    {
                        currentClonePos = cloneStart;
                    }
                    else if (StateTimer >= windupDuration && StateTimer <= windupDuration + dashDuration)
                    {
                        float dashProgress = (StateTimer - windupDuration) / (float)dashDuration;
                        currentClonePos = Vector2.Lerp(cloneStart, cloneEnd, dashProgress);
                    }
                    else
                    {
                        currentClonePos = cloneEnd;
                    }

                    float cloneRot = slashDir.ToRotation();
                    Color cloneColor = Color.Lerp(Color.DeepPink, Color.Cyan, i / 3f) * (StateTimer >= windupDuration + dashDuration + 15 ? 0.3f : 0.9f);

                    int frameH = 56;
                    int frameW = 40;
                    Rectangle headSrc = new Rectangle(0, (frameIndex % 20) * frameH, headTex.Width, frameH);
                    Rectangle bodySrc = new Rectangle(0, 0, frameW, frameH);

                    Vector2 headOrig = new Vector2(headTex.Width / 2f, frameH / 2f);
                    Vector2 bodyOrig = new Vector2(frameW / 2f, frameH / 2f);

                    if (cloneWingTex != null)
                    {
                        int wingNumFrames = 4;
                        int wingFrameHeight = cloneWingTex.Height / wingNumFrames;
                        int wingFrame = (int)(Main.GlobalTimeWrappedHourly * 22f) % wingNumFrames;
                        Rectangle wingSource = new Rectangle(0, wingFrame * wingFrameHeight, cloneWingTex.Width, wingFrameHeight);
                        Vector2 wingOrigin = new Vector2(cloneWingTex.Width / 2f, wingFrameHeight / 2f);

                        spriteBatch.Draw(cloneWingTex, currentClonePos, wingSource, cloneColor, cloneRot, wingOrigin, NPC.scale, spriteEffects, 0f);
                    }

                    spriteBatch.Draw(legTex, currentClonePos, headSrc, cloneColor, cloneRot, headOrig, NPC.scale, spriteEffects, 0f);
                    spriteBatch.Draw(bodyTex, currentClonePos, bodySrc, cloneColor, cloneRot, bodyOrig, NPC.scale, spriteEffects, 0f);
                    spriteBatch.Draw(headTex, currentClonePos, headSrc, cloneColor, cloneRot, headOrig, NPC.scale, spriteEffects, 0f);
                }

                if (StateTimer >= windupDuration && bloomLineTex?.Value != null)
                {
                    Texture2D lineTex = bloomLineTex.Value;
                    Vector2 lineOrigin = new Vector2(0f, lineTex.Height * 0.5f);

                    float trailAlpha = 1f - MathHelper.Clamp((StateTimer - windupDuration) / (float)(dashDuration + 25), 0f, 1f);

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                    for (int i = 0; i < 2; i++)
                    {
                        float lineAngle = MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                        Vector2 lineStart = centerDrawPos - lineAngle.ToRotationVector2() * 800f;

                        Color outerSlashCol = Color.Lerp(Color.DeepPink, Color.Magenta, (float)Math.Sin(time * 15f + i) * 0.5f + 0.5f) * trailAlpha * 0.95f;
                        Vector2 outerSlashScale = new Vector2(1600f / lineTex.Width, 3.8f * trailAlpha);
                        spriteBatch.Draw(lineTex, lineStart, null, outerSlashCol, lineAngle, lineOrigin, outerSlashScale, SpriteEffects.None, 0f);

                        Color coreSlashCol = Color.White * trailAlpha;
                        Vector2 coreSlashScale = new Vector2(1600f / lineTex.Width, 1.2f * trailAlpha);
                        spriteBatch.Draw(lineTex, lineStart, null, coreSlashCol, lineAngle, lineOrigin, coreSlashScale, SpriteEffects.None, 0f);
                    }

                    if (shineFlareTex?.Value != null)
                    {
                        Texture2D flare = shineFlareTex.Value;
                        Vector2 flareOrig = flare.Size() * 0.5f;

                        Color centerFlareCol = Color.Cyan * trailAlpha;
                        spriteBatch.Draw(flare, centerDrawPos, null, centerFlareCol, time * 12f, flareOrig, 3.5f * trailAlpha, SpriteEffects.None, 0f);
                        spriteBatch.Draw(flare, centerDrawPos, null, Color.White * trailAlpha, -time * 10f, flareOrig, 2.2f * trailAlpha, SpriteEffects.None, 0f);
                    }

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }
            }

            // ==================== 5. TRAIL EFFECT ====================
            bool isHighSpeed = State == AIState.Dash || State == AIState.WormholeDash || State == AIState.PrismMirage || State == AIState.SerpentPortalDash;
            if (isHighSpeed)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                if (bloomLineTex?.Value != null && NPC.oldPos.Length > 1)
                {
                    Texture2D lineTex = bloomLineTex.Value;
                    Vector2 lineOrigin = new Vector2(0f, lineTex.Height * 0.5f);

                    for (int k = 0; k < NPC.oldPos.Length - 1; k++)
                    {
                        if (NPC.oldPos[k] == Vector2.Zero || NPC.oldPos[k + 1] == Vector2.Zero) continue;

                        Vector2 start = NPC.oldPos[k] + NPC.Size * 0.5f - screenPos;
                        Vector2 end = NPC.oldPos[k + 1] + NPC.Size * 0.5f - screenPos;
                        Vector2 diff = end - start;
                        float length = diff.Length();
                        if (length < 2f) continue;

                        float trailProgress = k / (float)NPC.oldPos.Length;
                        float trailAlpha = (1f - trailProgress) * 0.7f;

                        Color trailColor = Color.Lerp(Color.Cyan, Color.DeepPink, trailProgress) * trailAlpha;
                        if (State == AIState.Dash)
                        {
                            trailColor = Color.Lerp(Color.Magenta, Color.MediumPurple, trailProgress) * trailAlpha;
                        }

                        Vector2 scale = new Vector2(length / lineTex.Width, 0.5f * (1f - trailProgress));
                        spriteBatch.Draw(lineTex, start, null, trailColor, diff.ToRotation(), lineOrigin, scale, SpriteEffects.None, 0f);
                    }
                }

                for (int i = 1; i < NPC.oldPos.Length; i += 2)
                {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;

                    Vector2 oldDrawPos = NPC.oldPos[i] + (NPC.Size * 0.5f) - screenPos + new Vector2(0f, NPC.gfxOffY + 4f);
                    float progress = i / (float)NPC.oldPos.Length;
                    float alphaMult = (1f - progress) * 0.5f;

                    Color afterimageColor = Color.Lerp(Color.Turquoise, Color.Magenta, progress) * alphaMult;
                    float oldRot = NPC.oldRot[i];
                    Vector2 headOrigin = new Vector2(headTex.Width / 2f, 56 / 2f);

                    spriteBatch.Draw(headTex, oldDrawPos, new Rectangle(0, (frameIndex % 20) * 56, headTex.Width, 56), afterimageColor, oldRot, headOrigin, NPC.scale, spriteEffects, 0f);
                }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // ==================== 6. TELEGRAPH LINE ====================
            bool isDashing = (State == AIState.Dash && StateTimer < 35) || (State == AIState.WormholeDash && StateTimer < 28) || (State == AIState.SerpentPortalDash && StateTimer < 45);
            if (isDashing && telegraphTexture?.Value != null)
            {
                Texture2D telegraphTex = telegraphTexture.Value;
                float rotation = dashTargetDir.ToRotation();

                float maxTimer = (State == AIState.WormholeDash) ? 28f : (State == AIState.SerpentPortalDash ? 45f : 35f);
                float alpha = (StateTimer / maxTimer) * ((255 - NPC.alpha) / 255f);
                float pulse = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 25f) * 0.3f;

                Color telegraphColor = Color.Lerp(Color.DeepSkyBlue, Color.HotPink, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.5f + 0.5f) * alpha * 0.9f;
                if (State == AIState.Dash)
                {
                    telegraphColor = Color.Magenta * alpha * 0.8f;
                }

                Vector2 lineStartPos = (State == AIState.WormholeDash || State == AIState.SerpentPortalDash) ? (portalExitPos - screenPos) : drawPos;
                Vector2 origin = new Vector2(0f, telegraphTex.Height / 2f);
                Vector2 scale = new Vector2(2200f / telegraphTex.Width, pulse);

                spriteBatch.Draw(telegraphTex, lineStartPos, null, telegraphColor, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            // ==================== 7. WINGS & BOSS MAIN SPRITE ====================
            if (NPC.alpha < 255)
            {
                // ---- 7a. Ambient Aura (glow tetap menyala, memberi kesan boss "hidup") ----
                if (bloomCircleTex?.Value != null)
                {
                    float visibility = (255 - NPC.alpha) / 255f;
                    float auraTime = (float)Main.GlobalTimeWrappedHourly;
                    float auraPulse = 1f + (float)Math.Sin(auraTime * 3f) * 0.12f;

                    // Palet berbeda antara Phase 1 (cyan/magenta) & Phase 2 (crimson/purple) agar transisi fase terasa
                    Color auraColor = isPhase2
                        ? Color.Lerp(Color.Crimson, Color.Purple, (float)Math.Sin(auraTime * 2f) * 0.5f + 0.5f)
                        : Color.Lerp(Color.Cyan, Color.Magenta, (float)Math.Sin(auraTime * 2f) * 0.5f + 0.5f);

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                    Texture2D auraCircle = bloomCircleTex.Value;
                    Vector2 auraOrigin = auraCircle.Size() * 0.5f;
                    spriteBatch.Draw(auraCircle, drawPos, null, auraColor * visibility * 0.35f, 0f, auraOrigin, 1.1f * auraPulse, SpriteEffects.None, 0f);

                    if (shineFlareTex?.Value != null)
                    {
                        Texture2D auraFlare = shineFlareTex.Value;
                        Vector2 flareOrigin = auraFlare.Size() * 0.5f;
                        spriteBatch.Draw(auraFlare, drawPos, null, Color.White * visibility * 0.18f, auraTime * 2.5f, flareOrigin, 0.55f * auraPulse, SpriteEffects.None, 0f);
                    }

                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }

                int wingSlot = ContentSamples.ItemsByType[ItemID.SpookyWings].wingSlot;
                Main.instance.LoadWings(wingSlot);

                if (TextureAssets.Wings[wingSlot]?.Value != null)
                {
                    Texture2D wingTex = TextureAssets.Wings[wingSlot].Value;
                    int wingNumFrames = 4;
                    int wingFrameHeight = wingTex.Height / wingNumFrames;

                    float flapSpeed = (State == AIState.Dash || State == AIState.WormholeDash || State == AIState.PrismMirage || State == AIState.PhantomGrid || State == AIState.SerpentPortalDash) ? 24f : 14f;
                    int wingFrame = (int)(Main.GlobalTimeWrappedHourly * flapSpeed) % wingNumFrames;

                    Rectangle wingSource = new Rectangle(0, wingFrame * wingFrameHeight, wingTex.Width, wingFrameHeight);
                    Vector2 wingOrigin = new Vector2(wingTex.Width / 2f, wingFrameHeight / 2f);
                    Vector2 wingDrawPos = drawPos + new Vector2(0f, -2f);

                    spriteBatch.Draw(wingTex, wingDrawPos, wingSource, alphaDrawColor, NPC.rotation, wingOrigin, NPC.scale, spriteEffects, 0f);
                }

                int frameHeight = 56;
                int frameWidth = 40;

                Rectangle headSource = new Rectangle(0, (frameIndex % 20) * frameHeight, headTex.Width, frameHeight);
                Rectangle legSource = new Rectangle(0, (frameIndex % 20) * frameHeight, legTex.Width, frameHeight);

                Vector2 headOrigin2 = new Vector2(headTex.Width / 2f, frameHeight / 2f);
                Vector2 legOrigin = new Vector2(legTex.Width / 2f, frameHeight / 2f);
                Vector2 bodyOrigin = new Vector2(frameWidth / 2f, frameHeight / 2f);

                Rectangle bodyTorsoSource = (frameIndex == 5 || State == AIState.Dash || State == AIState.WormholeDash || State == AIState.PrismMirage || State == AIState.PhantomGrid || State == AIState.SerpentPortalDash) ? new Rectangle(40, 0, frameWidth, frameHeight) : new Rectangle(0, 0, frameWidth, frameHeight);

                Rectangle bodyArmSource = Rectangle.Empty;
                if (bodyTex.Width >= 120)
                {
                    int armCol = frameIndex % 8;
                    int armRow = frameIndex / 8;
                    int armX = 80 + (armCol * frameWidth);
                    int armY = armRow * frameHeight;

                    if (armX + frameWidth <= bodyTex.Width && armY + frameHeight <= bodyTex.Height)
                    {
                        bodyArmSource = new Rectangle(armX, armY, frameWidth, frameHeight);
                    }
                }

                spriteBatch.Draw(legTex, drawPos, legSource, alphaDrawColor, NPC.rotation, legOrigin, NPC.scale, spriteEffects, 0f);
                spriteBatch.Draw(bodyTex, drawPos, bodyTorsoSource, alphaDrawColor, NPC.rotation, bodyOrigin, NPC.scale, spriteEffects, 0f);

                if (bodyArmSource != Rectangle.Empty)
                {
                    spriteBatch.Draw(bodyTex, drawPos, bodyArmSource, alphaDrawColor, NPC.rotation, bodyOrigin, NPC.scale, spriteEffects, 0f);
                }

                spriteBatch.Draw(headTex, drawPos, headSource, alphaDrawColor, NPC.rotation, headOrigin2, NPC.scale, spriteEffects, 0f);
            }

            return false;
        }

        // ==================== CUTSCENE HELPERS ====================
        // Robekan realitas terpakai bareng buat intro (Awakening) & death sequence -
        // sengaja disatukan biar bahasa visualnya konsisten (boss "datang" & "pergi" lewat cara yang sama).
        private void DrawRealityTear(SpriteBatch spriteBatch, Vector2 screenPos, Vector2 worldPos, float openness, Color mainColor)
        {
            if (telegraphTexture?.Value == null || openness <= 0f)
                return;

            Texture2D tex = telegraphTexture.Value;
            Vector2 drawPos = worldPos - screenPos;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            float time = (float)Main.GlobalTimeWrappedHourly;
            float pulse = 1f + (float)Math.Sin(time * 20f) * 0.18f;
            float slitHeight = MathHelper.Lerp(0f, 300f, openness) * pulse;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // ---- Halo bulat berlapis, berputar dua arah biar kesannya "hidup" ----
            if (bloomCircleTex?.Value != null)
            {
                Texture2D bloom = bloomCircleTex.Value;
                Vector2 bloomOrigin = bloom.Size() * 0.5f;

                spriteBatch.Draw(bloom, drawPos, null, mainColor * openness * 0.30f, time * 0.6f, bloomOrigin, 1.3f * openness * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(bloom, drawPos, null, Color.White * openness * 0.15f, -time * 0.9f, bloomOrigin, 0.85f * openness * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(bloom, drawPos, null, mainColor * openness * 0.6f, 0f, bloomOrigin, 0.55f * openness * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(bloom, drawPos, null, Color.White * openness * 0.5f, 0f, bloomOrigin, 0.22f * openness * pulse, SpriteEffects.None, 0f);
            }

            // ---- Retakan-retakan menjalar dari pusat, khas kaca realitas pecah ----
            int crackCount = 12;
            for (int i = 0; i < crackCount; i++)
            {
                float seed = i * 137.5f; // sebaran semi-acak tapi stabil (golden angle)
                float wobble = (float)Math.Sin(time * 3f + seed) * 0.5f;
                float crackAngle = MathHelper.PiOver2 + wobble + (i - crackCount / 2f) * 0.28f;
                float flicker = 0.6f + 0.4f * (float)Math.Sin(time * 7f + seed);
                float crackLen = (70f + (i % 4) * 35f) * openness * flicker;
                Vector2 crackScale = new Vector2(crackLen / tex.Width, 0.03f);

                Color crackColor = (i % 2 == 0) ? Color.White : mainColor;
                spriteBatch.Draw(tex, drawPos, null, crackColor * openness * 0.75f, crackAngle, origin, crackScale, SpriteEffects.None, 0f);
            }

            // ---- Robekan utama: beberapa lapis warna (fake chromatic aberration) + inti putih ----
            Vector2 mainScale = new Vector2(slitHeight / tex.Width, 0.26f + openness * 0.16f);

            spriteBatch.Draw(tex, drawPos + new Vector2(-3f, 0f), null, new Color(255, 60, 90) * openness * 0.45f, MathHelper.PiOver2, origin, mainScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos + new Vector2(3f, 0f), null, new Color(60, 180, 255) * openness * 0.45f, MathHelper.PiOver2, origin, mainScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, mainColor * openness, MathHelper.PiOver2, origin, mainScale * new Vector2(1f, 1.7f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, drawPos, null, Color.White * openness, MathHelper.PiOver2, origin, mainScale, SpriteEffects.None, 0f);

            // ---- Percikan mini di ujung atas & bawah robekan ----
            if (bloomCircleTex?.Value != null && openness > 0.1f)
            {
                Texture2D bloom = bloomCircleTex.Value;
                Vector2 bloomOrigin = bloom.Size() * 0.5f;
                Vector2 tipOffset = new Vector2(0f, slitHeight * 0.5f);

                spriteBatch.Draw(bloom, drawPos - tipOffset, null, Color.White * openness * 0.6f, 0f, bloomOrigin, 0.16f * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(bloom, drawPos + tipOffset, null, Color.White * openness * 0.6f, 0f, bloomOrigin, 0.16f * pulse, SpriteEffects.None, 0f);
            }

            // ---- Shine flare berputar dua arah - aksen kilau utama ----
            if (shineFlareTex?.Value != null)
            {
                Texture2D flare = shineFlareTex.Value;
                Vector2 flareOrigin = flare.Size() * 0.5f;

                spriteBatch.Draw(flare, drawPos, null, Color.White * openness * 0.85f, time * 3f, flareOrigin, 0.5f * openness * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(flare, drawPos, null, mainColor * openness * 0.65f, -time * 1.6f, flareOrigin, 0.75f * openness * pulse, SpriteEffects.None, 0f);
                spriteBatch.Draw(flare, drawPos, null, Color.White * openness * 0.4f, time * 5f, flareOrigin, 0.28f * openness * pulse, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Efek "mengetik" pesan terakhir boss di atas kepalanya sesaat sebelum dia masuk portal.
        private void DrawDeathTypingText(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            string fullMessage = CurrentDeathMessage;
            int visibleChars = DeathVisibleCharCount();
            if (visibleChars <= 0)
                return;

            string shown = fullMessage.Substring(0, visibleChars);

            bool stillTyping = visibleChars < fullMessage.Length;
            bool cursorBlinkOn = (int)(Main.GlobalTimeWrappedHourly * 6f) % 2 == 0;
            if (stillTyping && cursorBlinkOn)
                shown += "_";

            var font = FontAssets.MouseText.Value;
            Vector2 textSize = font.MeasureString(shown);
            Vector2 textPos = NPC.Center - screenPos + new Vector2(0f, -100f) - new Vector2(textSize.X * 0.5f, textSize.Y * 0.5f);

            // Jitter glitch halus - teks sesekali "meloncat" dikit, kesan gak stabil
            Vector2 jitter = Main.rand.NextFloat() < 0.06f ? Main.rand.NextVector2Circular(2f, 1.5f) : Vector2.Zero;

            Color textColor = isPhase2 ? Color.Lerp(Color.Crimson, Color.White, 0.3f) : Color.Lerp(Color.Cyan, Color.White, 0.3f);

            Utils.DrawBorderString(spriteBatch, shown, textPos + jitter, textColor, 1f);
        }
    }
}