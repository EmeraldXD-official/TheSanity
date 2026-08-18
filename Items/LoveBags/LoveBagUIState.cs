using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TheSanity.Items
{
    public class LoveBagUIState : UIState
    {
        // ---- grid tuning ----
        private const int SlotPadding = 6;
        private const int MinSlotSize = 44;
        private const int MaxSlotSize = 64;
        private const int MaxColumnsCap = 8;
        private const float GridAreaWidth = 620f; // matches gachaArea/fixedArea Width (-20 within a 640-wide panel)

        private UIElement mainPanel;
        private UIElement gachaArea;
        private UIElement fixedArea;
        private LoveButtonElement startButton;
        private LoveButtonElement closeButton;
        private UIText gachaLabel;
        private UIText fixedLabel;
        private UIText statusText;

        private readonly List<LoveBagSlotElement> gachaSlotElements = new List<LoveBagSlotElement>();
        private readonly List<LoveBagSlotElement> fixedSlotElements = new List<LoveBagSlotElement>();

        private Item bagItemRef;
        private LoveBagRewardSet rewardSet;

        // ==== gacha phase machine ====
        // Idle -> RollingKit (all kit pieces flicker & settle together)
        //      -> RollingSolo (Boots / Arrow / Wings etc, one at a time)
        //      -> RevealingFixed (guaranteed rewards pop in one at a time)
        //      -> Finished (auto-close countdown)
        private enum GachaPhase { Idle, RollingKit, RollingSolo, RevealingFixed, Finished }
        private GachaPhase phase = GachaPhase.Idle;

        private readonly List<int> kitSlotIndices = new List<int>();
        private readonly List<int> soloSlotIndices = new List<int>();
        private int soloCursor;
        private int fixedCursor;
        private int fixedRevealTimer;
        private const int FixedRevealInterval = 14; // ticks between each guaranteed item popping in

        private int flickerTimer;
        private const int FlickerToggleInterval = 4; // ganti tiap 4 tick (~15x/detik di 60fps)
        private int flickerSwitchesRemaining;
        private int flickerCandidateIndex;
        private int autoCloseTimer = -1;

        // ==== background hati ====
        private struct HeartParticle
        {
            public Vector2 Position;
            public float Speed;
            public float Scale;
            public float Rotation;
            public float RotationSpeed;
            public float Alpha;
        }
        private readonly List<HeartParticle> hearts = new List<HeartParticle>();
        private readonly Random visualRand = new Random();

        // ---- adaptive grid layout ----
        private struct GridLayout
        {
            public int columns;
            public int rows;
            public int slotSize;
            public float rowWidth;
            public int count;
        }

        public override void OnInitialize()
        {
            mainPanel = new UIElement();
            mainPanel.Width.Set(640, 0f);
            mainPanel.Height.Set(540, 0f);
            mainPanel.HAlign = 0.5f;
            mainPanel.VAlign = 0.5f;
            Append(mainPanel);

            // ---------- Title ----------
            var title = new UIText("Love Bag", 1.3f) { HAlign = 0.5f };
            title.Top.Set(10, 0f);
            mainPanel.Append(title);

            // ---------- Close button (X), top right ----------
            closeButton = new LoveButtonElement("X", 1.1f, new Color(120, 25, 55), new Color(190, 55, 90));
            closeButton.Width.Set(30, 0f);
            closeButton.Height.Set(30, 0f);
            closeButton.Left.Set(-8, 0f);
            closeButton.Top.Set(8, 0f);
            closeButton.HAlign = 1f;
            closeButton.OnLeftClick += (evt, el) => CloseWithoutClaiming();
            mainPanel.Append(closeButton);

            // ---------- Label + area for GACHA slots (top) ----------
            gachaLabel = new UIText("Gacha Rolls - one prize per pair", 0.9f);
            gachaLabel.Left.Set(10, 0f);
            gachaLabel.Top.Set(48, 0f);
            mainPanel.Append(gachaLabel);

            gachaArea = new UIElement();
            gachaArea.Width.Set(-20, 1f);
            gachaArea.Left.Set(10, 0f);
            gachaArea.Top.Set(72, 0f);
            mainPanel.Append(gachaArea);

            // ---------- Label + area for FIXED (guaranteed) rewards ----------
            fixedLabel = new UIText("Guaranteed Rewards", 0.9f);
            fixedLabel.Left.Set(10, 0f);
            mainPanel.Append(fixedLabel);

            fixedArea = new UIElement();
            fixedArea.Width.Set(-20, 1f);
            fixedArea.Left.Set(10, 0f);
            mainPanel.Append(fixedArea);

            // ---------- Status text ----------
            // FIX: dulu Top percent=1f DIBARENGIN VAlign=1f, offset-nya ke-double-count
            // sama seperti bug closeButton/startButton lama. Sekarang pixel-only + VAlign.
            statusText = new UIText("", 0.85f) { HAlign = 0.5f };
            statusText.Top.Set(-46, 0f);
            statusText.VAlign = 1f;
            mainPanel.Append(statusText);

            // ---------- Start button (green), bottom right ----------
            startButton = new LoveButtonElement("START", 1.1f, new Color(35, 130, 70), new Color(60, 195, 110));
            startButton.Width.Set(140, 0f);
            startButton.Height.Set(44, 0f);
            startButton.Left.Set(-14, 0f);
            startButton.Top.Set(-14, 0f);
            startButton.HAlign = 1f;
            startButton.VAlign = 1f;
            startButton.OnLeftClick += (evt, el) => BeginGacha();
            mainPanel.Append(startButton);

            // ---------- Background heart particles ----------
            for (int i = 0; i < 20; i++)
            {
                hearts.Add(new HeartParticle
                {
                    Position = new Vector2((float)visualRand.NextDouble() * 640, (float)visualRand.NextDouble() * 540),
                    Speed = 0.15f + (float)visualRand.NextDouble() * 0.35f,
                    Scale = 0.5f + (float)visualRand.NextDouble() * 0.6f,
                    Rotation = (float)(visualRand.NextDouble() * MathHelper.TwoPi),
                    RotationSpeed = ((float)visualRand.NextDouble() - 0.5f) * 0.02f,
                    Alpha = 0.2f + (float)visualRand.NextDouble() * 0.3f
                });
            }
        }

        public void Setup(Item bagItem, LoveBagRewardSet rewards)
        {
            bagItemRef = bagItem;
            rewardSet = rewards;
            phase = GachaPhase.Idle;
            soloCursor = 0;
            fixedCursor = 0;
            autoCloseTimer = -1;
            startButton.Disabled = false;
            statusText.SetText("Press START to open your Love Bag!");

            BuildSlots();
        }

        // ===================== LAYOUT =====================

        private GridLayout ComputeGrid(int count)
        {
            var grid = new GridLayout { count = count };
            if (count <= 0)
            {
                grid.columns = 1;
                grid.rows = 0;
                grid.slotSize = MinSlotSize;
                grid.rowWidth = 0f;
                return grid;
            }

            int maxColumnsForWidth = Math.Max(1, (int)((GridAreaWidth + SlotPadding) / (MinSlotSize + SlotPadding)));
            int maxColumns = Math.Min(MaxColumnsCap, Math.Min(maxColumnsForWidth, count));

            int rows = (int)Math.Ceiling(count / (float)maxColumns);
            int columns = (int)Math.Ceiling(count / (float)rows);
            columns = Math.Max(1, Math.Min(columns, maxColumns));

            int slotSize = (int)((GridAreaWidth - (columns - 1) * SlotPadding) / columns);
            slotSize = Math.Max(MinSlotSize, Math.Min(MaxSlotSize, slotSize));

            grid.columns = columns;
            grid.rows = rows;
            grid.slotSize = slotSize;
            grid.rowWidth = columns * slotSize + Math.Max(0, columns - 1) * SlotPadding;
            return grid;
        }

        private void PositionInGrid(UIElement element, int index, GridLayout grid)
        {
            int row = index / grid.columns;
            int col = index % grid.columns;
            int itemsInRow = Math.Min(grid.columns, grid.count - row * grid.columns);
            float rowWidth = itemsInRow * grid.slotSize + Math.Max(0, itemsInRow - 1) * SlotPadding;

            // center each row, and center the whole (possibly narrower) grid block within the area
            float blockOffsetX = (GridAreaWidth - grid.rowWidth) / 2f;
            float rowOffsetX = blockOffsetX + (grid.rowWidth - rowWidth) / 2f;

            element.Left.Set(rowOffsetX + col * (grid.slotSize + SlotPadding), 0f);
            element.Top.Set(row * (grid.slotSize + SlotPadding), 0f);
        }

        private void BuildSlots()
        {
            gachaArea.RemoveAllChildren();
            gachaSlotElements.Clear();
            fixedArea.RemoveAllChildren();
            fixedSlotElements.Clear();

            var gachaGrid = ComputeGrid(rewardSet.GachaSlots.Count);
            for (int i = 0; i < rewardSet.GachaSlots.Count; i++)
            {
                var slot = new LoveBagSlotElement(gachaGrid.slotSize, gachaGrid.slotSize);
                PositionInGrid(slot, i, gachaGrid);

                var firstCandidate = rewardSet.GachaSlots[i].Candidates[0];
                slot.SetDisplay(firstCandidate.itemType, firstCandidate.stack);
                slot.Kind = LoveBagRewardKind.Gacha;
                slot.State = LoveBagSlotVisualState.Preview;

                gachaArea.Append(slot);
                gachaSlotElements.Add(slot);
            }

            var fixedGrid = ComputeGrid(rewardSet.FixedRewards.Count);
            for (int i = 0; i < rewardSet.FixedRewards.Count; i++)
            {
                var slot = new LoveBagSlotElement(fixedGrid.slotSize, fixedGrid.slotSize);
                PositionInGrid(slot, i, fixedGrid);

                slot.Kind = LoveBagRewardKind.Fixed;
                slot.State = LoveBagSlotVisualState.Hidden; // revealed one-by-one once all gacha is done

                fixedArea.Append(slot);
                fixedSlotElements.Add(slot);
            }

            RelayoutPanel(gachaGrid, fixedGrid);
        }

        // Panel and section heights adapt to however many slots this particular bag ended up with
        // (fixed reward count varies with which mods are loaded), instead of a fixed, sometimes-empty box.
        private void RelayoutPanel(GridLayout gachaGrid, GridLayout fixedGrid)
        {
            const float gachaTop = 72f;
            float gachaHeight = gachaGrid.rows * gachaGrid.slotSize + Math.Max(0, gachaGrid.rows - 1) * SlotPadding;
            gachaArea.Height.Set(gachaHeight, 0f);

            float fixedLabelTop = gachaTop + gachaHeight + 26f;
            fixedLabel.Top.Set(fixedLabelTop, 0f);

            float fixedTop = fixedLabelTop + 22f;
            fixedArea.Top.Set(fixedTop, 0f);

            float fixedHeight = fixedGrid.rows * fixedGrid.slotSize + Math.Max(0, fixedGrid.rows - 1) * SlotPadding;
            fixedArea.Height.Set(fixedHeight, 0f);

            float panelHeight = Math.Max(fixedTop + fixedHeight + 76f, 340f);
            mainPanel.Height.Set(panelHeight, 0f);
        }

        // ===================== GACHA FLOW =====================

        private void BeginGacha()
        {
            if (phase != GachaPhase.Idle || rewardSet == null) return;

            kitSlotIndices.Clear();
            soloSlotIndices.Clear();
            for (int i = 0; i < rewardSet.GachaSlots.Count; i++)
            {
                if (rewardSet.GachaSlots[i].IsKitGroup)
                    kitSlotIndices.Add(i);
                else
                    soloSlotIndices.Add(i);
            }

            startButton.Disabled = true;
            soloCursor = 0;
            statusText.SetText("");

            if (kitSlotIndices.Count > 0)
            {
                phase = GachaPhase.RollingKit;
                StartFlicker(30);
                foreach (int idx in kitSlotIndices)
                    gachaSlotElements[idx].State = LoveBagSlotVisualState.Rolling;
            }
            else if (soloSlotIndices.Count > 0)
            {
                phase = GachaPhase.RollingSolo;
                StartFlicker(16);
                gachaSlotElements[soloSlotIndices[0]].State = LoveBagSlotVisualState.Rolling;
            }
            else
            {
                BeginFixedReveal();
            }
        }

        private void StartFlicker(int switches)
        {
            flickerTimer = 0;
            flickerCandidateIndex = 0;
            flickerSwitchesRemaining = switches;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateHearts();

            switch (phase)
            {
                case GachaPhase.RollingKit:
                    UpdateKitRoll();
                    break;
                case GachaPhase.RollingSolo:
                    UpdateSoloRoll();
                    break;
                case GachaPhase.RevealingFixed:
                    UpdateFixedReveal();
                    break;
            }

            if (phase == GachaPhase.Finished && autoCloseTimer >= 0)
            {
                autoCloseTimer--;
                int secondsLeft = autoCloseTimer / 60 + 1;
                statusText.SetText($"All rewards claimed! Closing in {secondsLeft}...");

                if (autoCloseTimer <= 0)
                {
                    FinalizeAndClose();
                }
            }
        }

        private void UpdateHearts()
        {
            for (int i = 0; i < hearts.Count; i++)
            {
                var h = hearts[i];
                h.Position.Y -= h.Speed;
                h.Rotation += h.RotationSpeed;
                if (h.Position.Y < -20) h.Position.Y = 560;
                hearts[i] = h;
            }
        }

        // Kit pieces (armor/weapons/etc) flicker and settle TOGETHER - one big reveal moment.
        private void UpdateKitRoll()
        {
            flickerTimer++;
            if (flickerTimer < FlickerToggleInterval) return;
            flickerTimer = 0;
            flickerSwitchesRemaining--;

            if (flickerSwitchesRemaining > 0)
            {
                flickerCandidateIndex++;
                foreach (int idx in kitSlotIndices)
                {
                    var data = rewardSet.GachaSlots[idx];
                    var cand = data.Candidates[flickerCandidateIndex % data.Candidates.Count];
                    var el = gachaSlotElements[idx];
                    el.SetDisplay(cand.itemType, cand.stack);
                    el.State = LoveBagSlotVisualState.Rolling;
                }
                SoundEngine.PlaySound(SoundID.Item95);
            }
            else
            {
                foreach (int idx in kitSlotIndices)
                {
                    var winner = rewardSet.GachaSlots[idx].Winner;
                    var el = gachaSlotElements[idx];
                    el.SetDisplay(winner.itemType, winner.stack);
                    el.State = LoveBagSlotVisualState.Revealed;
                    el.Kind = LoveBagRewardKind.Gacha;
                    el.PlayRevealBurst();
                }
                SoundEngine.PlaySound(SoundID.Item4);

                if (soloSlotIndices.Count > 0)
                {
                    phase = GachaPhase.RollingSolo;
                    soloCursor = 0;
                    StartFlicker(16);
                    gachaSlotElements[soloSlotIndices[0]].State = LoveBagSlotVisualState.Rolling;
                }
                else
                {
                    BeginFixedReveal();
                }
            }
        }

        // Independent slots (Boots / Arrow / Wings...) are resolved one at a time, in order.
        private void UpdateSoloRoll()
        {
            int idx = soloSlotIndices[soloCursor];
            var data = rewardSet.GachaSlots[idx];
            var el = gachaSlotElements[idx];

            flickerTimer++;
            if (flickerTimer < FlickerToggleInterval) return;
            flickerTimer = 0;
            flickerSwitchesRemaining--;

            if (flickerSwitchesRemaining > 0)
            {
                flickerCandidateIndex = (flickerCandidateIndex + 1) % data.Candidates.Count;
                var cand = data.Candidates[flickerCandidateIndex];
                el.SetDisplay(cand.itemType, cand.stack);
                el.State = LoveBagSlotVisualState.Rolling;
                SoundEngine.PlaySound(SoundID.Item95);
            }
            else
            {
                var winner = data.Winner;
                el.SetDisplay(winner.itemType, winner.stack);
                el.State = LoveBagSlotVisualState.Revealed;
                el.Kind = LoveBagRewardKind.Gacha;
                el.PlayRevealBurst();
                SoundEngine.PlaySound(SoundID.Item4);

                soloCursor++;
                if (soloCursor < soloSlotIndices.Count)
                {
                    StartFlicker(16);
                    gachaSlotElements[soloSlotIndices[soloCursor]].State = LoveBagSlotVisualState.Rolling;
                }
                else
                {
                    BeginFixedReveal();
                }
            }
        }

        // Guaranteed rewards pop in one-by-one once every gacha slot has settled.
        // Gacha winners are already fully revealed by now, so THEY get granted (and the bag
        // gets consumed) right as this phase begins. Each fixed/guaranteed item, on the other
        // hand, is only granted the moment its own slot actually pops open (see UpdateFixedReveal) -
        // no more getting the whole bag's contents before you've even seen them reveal.
        private void BeginFixedReveal()
        {
            phase = GachaPhase.RevealingFixed;
            fixedCursor = 0;
            fixedRevealTimer = FixedRevealInterval; // reveal the first one almost immediately

            GrantGachaRewardsAndConsumeBag();

            if (fixedSlotElements.Count == 0)
            {
                FinishAllResolved();
            }
        }

        private void UpdateFixedReveal()
        {
            fixedRevealTimer++;
            if (fixedRevealTimer < FixedRevealInterval) return;
            fixedRevealTimer = 0;

            var el = fixedSlotElements[fixedCursor];
            var reward = rewardSet.FixedRewards[fixedCursor];
            el.SetDisplay(reward.ItemType, reward.Stack);
            el.State = LoveBagSlotVisualState.Revealed;
            el.Kind = LoveBagRewardKind.Fixed;
            el.PlayRevealBurst();
            SoundEngine.PlaySound(SoundID.Item4);

            // Grant THIS guaranteed item right as its slot opens, not before.
            Player player = Main.LocalPlayer;
            var source = player.GetSource_OpenItem(bagItemRef?.type ?? 0);
            player.QuickSpawnItem(source, reward.ItemType, reward.Stack);

            fixedCursor++;
            if (fixedCursor >= fixedSlotElements.Count)
            {
                FinishAllResolved();
            }
        }

        private void FinishAllResolved()
        {
            phase = GachaPhase.Finished;
            autoCloseTimer = 180; // 3 detik (60 tick/detik)
        }

        private void GrantGachaRewardsAndConsumeBag()
        {
            Player player = Main.LocalPlayer;
            var source = player.GetSource_OpenItem(bagItemRef?.type ?? 0);

            foreach (var gachaSlot in rewardSet.GachaSlots)
            {
                var winner = gachaSlot.Winner;
                player.QuickSpawnItem(source, winner.itemType, winner.stack);
            }

            // Konsumsi Love Bag manual (auto-consume dimatikan lewat ModItem.ConsumeItem => false).
            if (bagItemRef != null)
            {
                bagItemRef.stack -= 1;
                if (bagItemRef.stack <= 0)
                {
                    bagItemRef.TurnToAir();
                }
            }
        }

        private void CloseWithoutClaiming()
        {
            if (phase == GachaPhase.Idle)
            {
                ModContent.GetInstance<LoveBagUISystem>().CloseLoveBagUI();
                return;
            }
            if (phase == GachaPhase.Finished)
            {
                FinalizeAndClose();
                return;
            }
            // Mid-roll: ignore, can't back out once the gacha is already in motion.
        }

        private void FinalizeAndClose()
        {
            ModContent.GetInstance<LoveBagUISystem>().CloseLoveBagUI();
        }

        // ===================== DRAW =====================

        public override void Draw(SpriteBatch spriteBatch)
        {
            DrawLoveBackground(spriteBatch);
            base.Draw(spriteBatch);

            if (mainPanel.ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }
        }

        private void DrawLoveBackground(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = mainPanel.GetDimensions();
            Rectangle panelRect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);

            // Panel dasar bernuansa pink/merah muda gelap, tema "Love", pakai gradient bukan flat fill.
            DrawVerticalGradient(spriteBatch, panelRect, new Color(85, 15, 45), new Color(45, 8, 28));

            // Hearts pakai icon vanilla ItemID.Heart, discatter & melayang pelan di background
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D heartTex = TextureAssets.Item[ItemID.Heart].Value;
            foreach (var h in hearts)
            {
                Vector2 drawPos = new Vector2(panelRect.X, panelRect.Y) + h.Position;
                if (panelRect.Contains(drawPos.ToPoint()))
                {
                    Vector2 origin = new Vector2(heartTex.Width, heartTex.Height) * 0.5f;
                    spriteBatch.Draw(heartTex, drawPos, null, Color.HotPink * h.Alpha,
                        h.Rotation, origin, h.Scale, SpriteEffects.None, 0f);
                }
            }

            // Border tipis biar panel keliatan jelas batasnya
            Color borderColor = new Color(255, 130, 180) * 0.65f;
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Bottom - 3, panelRect.Width, 3), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.X, panelRect.Y, 3, panelRect.Height), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panelRect.Right - 3, panelRect.Y, 3, panelRect.Height), borderColor);
        }

        private static void DrawVerticalGradient(SpriteBatch spriteBatch, Rectangle rect, Color top, Color bottom)
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            const int steps = 10;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)(steps - 1);
                Color c = Color.Lerp(top, bottom, t);
                int y = rect.Y + (int)(rect.Height * (i / (float)steps));
                int h = (int)Math.Ceiling(rect.Height / (float)steps) + 1;
                spriteBatch.Draw(pixel, new Rectangle(rect.X, y, rect.Width, h), c);
            }
        }

        // Small reusable button: gradient fill, border, brightens on hover, dims when Disabled.
        private class LoveButtonElement : UIElement
        {
            private readonly UIText label;
            public bool Disabled;

            private readonly Color baseColor;
            private readonly Color hoverColor;

            public LoveButtonElement(string text, float textScale, Color baseColor, Color hoverColor)
            {
                this.baseColor = baseColor;
                this.hoverColor = hoverColor;
                label = new UIText(text, textScale) { HAlign = 0.5f, VAlign = 0.5f };
                Append(label);
            }

            protected override void DrawSelf(SpriteBatch spriteBatch)
            {
                CalculatedStyle dims = GetDimensions();
                Rectangle rect = new Rectangle((int)dims.X, (int)dims.Y, (int)dims.Width, (int)dims.Height);
                bool hover = !Disabled && ContainsPoint(Main.MouseScreen);
                Texture2D pixel = TextureAssets.MagicPixel.Value;

                Color fillTop = Disabled ? baseColor * 0.35f : (hover ? hoverColor : baseColor);
                Color fillBottom = fillTop * 0.6f;
                DrawVerticalGradient(spriteBatch, rect, fillTop, fillBottom);

                Color border = Color.White * (Disabled ? 0.15f : (hover ? 0.75f : 0.4f));
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), border);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), border);
                spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), border);
                spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), border);

                if (hover)
                {
                    Main.LocalPlayer.mouseInterface = true;
                }
            }
        }
    }
}
