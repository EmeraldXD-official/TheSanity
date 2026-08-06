using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityMod.Systems.Mechanic
{
    public class ArenaWallSystem : ModSystem
    {
        public static List<Box> ActiveBoxes = [];
        public class Box
        {

            public Func<bool> RemovalCondition = () => false;

            public Vector2 position;

            public Vector4 boxDimensions;

            public Vector4 NewDimensions;

            public Vector2 NewPosition;

            public float borderThickness;

            public Color borderColor = Color.Red;
            public float DistanceUp => boxDimensions.X;
            public float DistanceRight => boxDimensions.Y;
            public float DistanceDown => boxDimensions.Z;
            public float DistanceLeft => boxDimensions.W;

            public Vector2 TopLeft => position + new Vector2(-DistanceLeft, -DistanceUp);
            public Vector2 TopRight => position + new Vector2(DistanceRight, -DistanceUp);
            public Vector2 BottomLeft => position + new Vector2(-DistanceLeft, DistanceDown);
            public Vector2 BottomRight => position + new Vector2(DistanceRight, DistanceDown);

            public Vector2 Center => (TopLeft + BottomRight) * 0.5f;

            public Vector2 Size => new Vector2(DistanceLeft + DistanceRight, DistanceUp + DistanceDown);

            public Vector4 Hitbox => new Vector4(TopLeft.X, TopLeft.Y, Size.X, Size.Y);
            public Rectangle HitboxRectangle => new Rectangle((int)TopLeft.X, (int)TopLeft.Y, (int)Size.X, (int)Size.Y);

            public Box oldData = null;

            public Action<Box> DrawBox = null;

            public Action<Box> UpdateBox = null;
            /// <summary>
            /// The despawn logic to kill this box. Only runs when RemovalCondition is true. Return true to delete the box from ActiveBoxes.
            /// </summary>
            public Func<Box, bool> DespawnAction = (box) => true;

            public bool PullPlayerWhenSizeChanged => false;
            public void DrawBoxWithOffset(float Offset, float Thickness, Color color)
            {

                CalamityUtils.DrawLineBetter(Main.spriteBatch, TopLeft + new Vector2(-(Offset + Thickness * 0.5f), -Offset), TopRight + new Vector2((Offset + Thickness * 0.5f), -Offset), color, Thickness);
                CalamityUtils.DrawLineBetter(Main.spriteBatch, BottomLeft + new Vector2(-(Offset + Thickness * 0.5f), Offset), BottomRight + new Vector2((Offset + Thickness * 0.5f), Offset), color, Thickness);
                CalamityUtils.DrawLineBetter(Main.spriteBatch, TopLeft + new Vector2(-Offset, -(Offset - Thickness * 0.5f)), BottomLeft + new Vector2(-Offset, (Offset - Thickness * 0.5f)), color, Thickness);
                CalamityUtils.DrawLineBetter(Main.spriteBatch, BottomRight + new Vector2(Offset, (Offset - Thickness * 0.5f)), TopRight + new Vector2(Offset, -(Offset - Thickness * 0.5f)), color, Thickness);
            }

            public void SetOldData()
            {
                if (oldData == null)
                    oldData = new Box() { borderColor = borderColor, borderThickness = borderThickness, boxDimensions = boxDimensions, position = position };
                else
                {
                    oldData.borderThickness = borderThickness;
                    oldData.boxDimensions = boxDimensions;
                    oldData.position = position;
                }
            }
            public bool Contains(Vector2 Position, Vector2 size)
            {
                return Collision.CheckAABBvAABBCollision(TopLeft - new Vector2(borderThickness * 0.5f), Size + new Vector2(borderThickness), Position, size);
            }

            public bool ShouldEffectPlayer(Player player) => Collision.CheckAABBvAABBCollision(TopLeft - new Vector2(borderThickness + 300), Size + new Vector2(borderThickness + 300) * 2, player.TopLeft, player.Size);
            public bool InnerEffect(Vector2 Position, Vector2 size) => Collision.CheckAABBvAABBCollision(TopLeft - new Vector2(borderThickness) * 0.5f, Size + new Vector2(borderThickness), Position, size);

            public bool PointInWall(Vector2 pos)
            {
                static bool Vector2PointCollision(Vector2 position, Vector2 size, Vector2 point)
                {
                    return (point.X >= position.X && point.X <= position.X + size.X && point.Y >= position.Y && point.Y <= position.Y + size.Y);
                }
                return !Vector2PointCollision(TopLeft, Size, pos) && Vector2PointCollision(TopLeft - new Vector2(borderThickness), Size + new Vector2(borderThickness) * 2, pos);
            }

            public bool Vector2PairInWall(Vector2 pos, Vector2 size)
            {
                return Collision.CheckAABBvAABBCollision(TopLeft - new Vector2(borderThickness), new Vector2(size.X + borderThickness * 2, borderThickness), pos, size)
                    || Collision.CheckAABBvAABBCollision(TopRight + new Vector2(0, -borderThickness), new Vector2(borderThickness, size.Y + borderThickness * 2), pos, size)
                    || Collision.CheckAABBvAABBCollision(BottomLeft + new Vector2(-borderThickness, 0), new Vector2(size.X + borderThickness * 2, borderThickness), pos, size)
                    || Collision.CheckAABBvAABBCollision(TopLeft - new Vector2(borderThickness), new Vector2(borderThickness, size.Y + borderThickness * 2), pos, size);
            }
        }
        public override void PostDrawTiles()
        {


            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.Transform
            );
            foreach (var box in ActiveBoxes)
            {
                if (box.DrawBox is not null)
                    box.DrawBox(box);
                else
                {
                    var color = Color.Black * 0.75f;
                    //Inside Fill
                    box.DrawBoxWithOffset(box.borderThickness * 0.5f, box.borderThickness, Color.Black * 0.75f);
                    //Inner Border
                    box.DrawBoxWithOffset(4, 8, box.borderColor);
                    //Outer Border
                    box.DrawBoxWithOffset(box.borderThickness - 4, 4, box.borderColor);
                }
            }
            Main.spriteBatch.End();
        }

        public override void PreUpdateEntities()
        {
            for (var i = 0; i < ActiveBoxes.Count; i++)
            {
                var box = ActiveBoxes[i];
                if (box.RemovalCondition())
                {
                    if (box.DespawnAction(box))
                    {
                        ActiveBoxes.Remove(box);
                        i--;
                    }

                    continue;
                }

                box.SetOldData();
                if (box.NewDimensions != Vector4.Zero)
                {
                    box.boxDimensions = box.NewDimensions;
                }
                if (box.NewPosition != Vector2.Zero)
                {
                    box.position = box.NewPosition;
                }
                if (box.UpdateBox != null)
                    box.UpdateBox(box);
            }
        }

        public override void OnWorldUnload()
        {
            ActiveBoxes = [];
        }
    }

    public class ArenaWallPlayer : ModPlayer
    {
        public Vector2 touchingSides = new Vector2();
        public override void PreUpdateMovement()
        {


            foreach (var box in ArenaWallSystem.ActiveBoxes)
            {
                if (box.ShouldEffectPlayer(Player))
                {
                    if (box.Contains(Player.position, Player.Size))
                        ContainPlayerLogic(box);
                }
            }
        }

        void ContainPlayerLogic(ArenaWallSystem.Box box)
        {
            if (box.oldData is not null && box.PullPlayerWhenSizeChanged)
            {
                if (touchingSides.X > 0)
                {
                    Player.position.Y += ((box.oldData.TopLeft.Y - box.oldData.BottomRight.Y) - (box.TopLeft.Y - box.BottomRight.Y)) * touchingSides.X;
                }

                if (touchingSides.Y > 0)
                {
                    Player.position.X += ((box.oldData.TopLeft.X - box.oldData.BottomRight.X) - (box.TopLeft.X - box.BottomRight.X)) * touchingSides.Y;
                }
            }

            touchingSides = Vector2.Zero;
            #region Snapping
            if (Player.Left.X < box.TopLeft.X)
            {
                Player.position.X = box.TopLeft.X;
                touchingSides.X = Utils.Remap(Player.Center.Y, box.TopLeft.Y, box.BottomRight.Y, 0, 1, true);
                touchingSides.Y = Utils.Remap(Player.Center.X, box.TopLeft.X, box.BottomRight.X, 0, 1, true);
            }
            if (Player.Right.X > box.BottomRight.X)
            {
                Player.position.X = box.BottomRight.X - Player.width;
                touchingSides.X = Utils.Remap(Player.Center.Y, box.TopLeft.Y, box.BottomRight.Y, 0, 1, true);
                touchingSides.Y = Utils.Remap(Player.Center.X, box.TopLeft.X, box.BottomRight.X, 0, 1, true);
            }
            if (Player.TopLeft.Y < box.TopLeft.Y)
            {
                Player.position.Y = box.TopLeft.Y;
                touchingSides.X = Utils.Remap(Player.Center.Y, box.TopLeft.Y, box.BottomRight.Y, 0, 1, true);
                touchingSides.Y = Utils.Remap(Player.Center.X, box.TopLeft.X, box.BottomRight.X, 0, 1, true);
            }
            if (Player.BottomRight.Y > box.BottomRight.Y)
            {
                Player.position.Y = box.BottomRight.Y - Player.height;
                touchingSides.X = Utils.Remap(Player.Center.Y, box.TopLeft.Y, box.BottomRight.Y, 0, 1, true);
                touchingSides.Y = Utils.Remap(Player.Center.X, box.TopLeft.X, box.BottomRight.X, 0, 1, true);
            }
            #endregion
            void spawnShoeDust()
            {

                int num4 = Dust.NewDust(new Vector2(Player.position.X + (float)(Player.width / 2) + (float)((Player.width / 2 - 4) * Player.slideDir), Player.position.Y + (float)(Player.height / 2) + (float)(Player.height / 2 - 4) * Player.gravDir), 8, 8, DustID.Smoke);
                if (Player.slideDir < 0)
                {
                    Main.dust[num4].position.X -= 10f;
                }
                if (Player.gravDir < 0f)
                {
                    Main.dust[num4].position.Y -= 12f;
                }
                Main.dust[num4].velocity *= 0.1f;
                Main.dust[num4].scale *= 1.2f;
                Main.dust[num4].noGravity = true;
                Main.dust[num4].shader = GameShaders.Armor.GetSecondaryShader(Player.cShoe, Player);
            }
            void applyShoeSpikes()
            {
                if (Player.spikedBoots == 0 || Player.velocity.Y < 0 || Player.mount.Active)
                    return;
                if (Player.controlDown && Player.spikedBoots > 0)
                {
                    Player.velocity.Y = 4f * Player.gravDir;
                    spawnShoeDust();
                }
                else if (Player.spikedBoots <= 2)
                {
                    Player.velocity.Y = 0;
                }
                else if (Player.spikedBoots == 1)
                {

                    Player.velocity.Y = 0.5f * Player.gravDir;
                    spawnShoeDust();
                }
            }
            #region Velocity

            var originalVelocity = Player.velocity;
            var originalTopLeft = Player.TopLeft;
            var originalBottomRight = Player.BottomRight;
            Player.position += originalVelocity;

            if (Player.Left.X < box.TopLeft.X)
            {
                Player.velocity.X = box.TopLeft.X - originalTopLeft.X;
                if (Player.controlLeft)
                {
                    Player.slideDir = -1;
                    applyShoeSpikes();
                }
            }
            if (Player.Right.X > box.BottomRight.X)
            {
                Player.velocity.X = box.BottomRight.X - originalBottomRight.X;
                Dust.NewDustPerfect(Player.Right, DustID.Smoke);
                if (Player.controlRight)
                {
                    Player.slideDir = 1;
                    applyShoeSpikes();
                }
            }
            if (Player.TopLeft.Y < box.TopLeft.Y)
            {

                Player.velocity.Y = box.TopLeft.Y - originalTopLeft.Y;
                if (Player.velocity.Y == 0)
                    Player.velocity.Y += 0.001f;
                touchingSides.Y = Utils.Remap(Player.Center.X, box.TopLeft.X, box.BottomRight.X, 0, 1, true);
            }

            if (Player.BottomRight.Y > box.BottomRight.Y)
            {
                Player.velocity.Y = box.BottomRight.Y - originalBottomRight.Y;
                touchingSides.Y = Utils.Remap(Player.Center.X, box.TopLeft.X, box.BottomRight.X, 0, 1, true);
            }
            Player.position -= originalVelocity;
            #endregion
        }
    }
}
