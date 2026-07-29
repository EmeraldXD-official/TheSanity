using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Tiles;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    public static class ReligiaSkyOreGen
    {
        private const int PlanetoidMinRadius = 8;
        private const int PlanetoidMaxRadius = 15;
        private const int EdgeMargin = 80;
        private const int MaxPlacementAttemptsPerPlanetoid = 40;
        private const int MinPlanetoidCount = 3;
        private const int MaxPlanetoidCount = 6;

        // Jarak minimum ekstra yang dijaga antar planetoid (di luar radius masing-masing).
        private const int PlanetoidSpacing = 14;

        private enum PlanetoidShape { Circular, Elongated, Blob }

        private struct PlacedPlanetoid {
            public int X, Y, MaxRadius;
        }

        /// <summary>
        /// Carves beberapa planetoid Religia (bentuk & ukuran bervariasi) di layer luar angkasa.
        /// Server/singleplayer only - dipanggil dari OnKill setelah kedua boss mati.
        /// </summary>
        public static void SpawnSkyPlanetoid() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int count = WorldGen.genRand.Next(MinPlanetoidCount, MaxPlanetoidCount + 1);
            List<PlacedPlanetoid> placed = new List<PlacedPlanetoid>();

            // Vanilla treats roughly the top ~35% of worldSurface as the "space" layer.
            int minY = 60;
            int maxY = (int)(Main.worldSurface * 0.35f);
            if (maxY - PlanetoidMaxRadius <= minY + PlanetoidMaxRadius) maxY = minY + PlanetoidMaxRadius * 2 + 10;

            for (int n = 0; n < count; n++) {
                int radius = WorldGen.genRand.Next(PlanetoidMinRadius, PlanetoidMaxRadius + 1);
                PlanetoidShape shape = (PlanetoidShape)WorldGen.genRand.Next(3);
                float aspect = shape == PlanetoidShape.Elongated ? WorldGen.genRand.NextFloat(0.5f, 0.75f) : 1f;
                float rotation = WorldGen.genRand.NextFloat(0f, MathHelper.TwoPi);

                // Blob spikes bisa nonjol sedikit lebih jauh dari radius dasarnya - dipakai buat
                // bounding check biar gak ketumpuk atau nabrak tile yang udah ada.
                int maxEffectiveRadius = (int)(radius * 1.15f);

                int centerX = 0, centerY = 0;
                bool found = false;

                for (int attempt = 0; attempt < MaxPlacementAttemptsPerPlanetoid; attempt++) {
                    int tryX = WorldGen.genRand.Next(EdgeMargin + maxEffectiveRadius, Main.maxTilesX - EdgeMargin - maxEffectiveRadius);
                    int tryY = WorldGen.genRand.Next(minY + maxEffectiveRadius, maxY);

                    if (!IsClearOfWorld(tryX, tryY, maxEffectiveRadius + 3)) continue;
                    if (!IsClearOfOtherPlanetoids(tryX, tryY, maxEffectiveRadius, placed)) continue;

                    centerX = tryX;
                    centerY = tryY;
                    found = true;
                    break;
                }

                // Kalau gak nemu spot yang aman setelah beberapa attempt, skip planetoid ini
                // daripada maksa nempel/numpuk sama yang lain.
                if (!found) continue;

                CarvePlanetoid(centerX, centerY, radius, shape, aspect, rotation);
                placed.Add(new PlacedPlanetoid { X = centerX, Y = centerY, MaxRadius = maxEffectiveRadius });

                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendTileSquare(-1, centerX, centerY, maxEffectiveRadius * 2 + 6);
                }
            }
        }

        private static bool IsClearOfOtherPlanetoids(int cx, int cy, int radius, List<PlacedPlanetoid> placed) {
            foreach (PlacedPlanetoid p in placed) {
                float dist = Vector2.Distance(new Vector2(cx, cy), new Vector2(p.X, p.Y));
                if (dist < radius + p.MaxRadius + PlanetoidSpacing) return false;
            }
            return true;
        }

        private static bool IsClearOfWorld(int cx, int cy, int radius) {
            for (int x = cx - radius; x <= cx + radius; x++) {
                for (int y = cy - radius; y <= cy + radius; y++) {
                    if (!WorldGen.InWorld(x, y, 10)) return false;

                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (dist > radius) continue;

                    Tile tile = Main.tile[x, y];
                    if (tile != null && tile.HasTile) return false;
                }
            }
            return true;
        }

        private static void CarvePlanetoid(int cx, int cy, int radius, PlanetoidShape shape, float aspect, float rotation) {
            int coreRadius = Math.Max(2, (int)(radius * 0.55f));
            float coreFraction = coreRadius / (float)radius;
            int oreTileType = ModContent.TileType<ReligiaOreTile>();
            int searchRadius = (int)(radius * 1.2f) + 2;

            // Noise per-sudut buat bentuk blob (radius efektif beda-beda tiap arah).
            float[] blobNoise = null;
            if (shape == PlanetoidShape.Blob) {
                int samples = 24;
                blobNoise = new float[samples];
                for (int i = 0; i < samples; i++) {
                    blobNoise[i] = WorldGen.genRand.NextFloat(0.7f, 1.15f);
                }
            }

            for (int x = cx - searchRadius; x <= cx + searchRadius; x++) {
                for (int y = cy - searchRadius; y <= cy + searchRadius; y++) {
                    if (!WorldGen.InWorld(x, y, 5)) continue;

                    float dx = x - cx;
                    float dy = y - cy;

                    bool inside;
                    float normalizedDist;

                    switch (shape) {
                        case PlanetoidShape.Elongated: {
                            float rx = dx * (float)Math.Cos(-rotation) - dy * (float)Math.Sin(-rotation);
                            float ry = dx * (float)Math.Sin(-rotation) + dy * (float)Math.Cos(-rotation);
                            float a = radius;
                            float b = radius * aspect;
                            float ellipseVal = (rx * rx) / (a * a) + (ry * ry) / (b * b);
                            float edgeNoise = (float)WorldGen.genRand.NextDouble() * 0.06f;
                            inside = ellipseVal <= 1f - edgeNoise;
                            normalizedDist = (float)Math.Sqrt(Math.Max(0f, ellipseVal));
                            break;
                        }
                        case PlanetoidShape.Blob: {
                            float angle = (float)Math.Atan2(dy, dx);
                            if (angle < 0) angle += MathHelper.TwoPi;
                            float sampleF = angle / MathHelper.TwoPi * blobNoise.Length;
                            int i0 = (int)sampleF % blobNoise.Length;
                            int i1 = (i0 + 1) % blobNoise.Length;
                            float t = sampleF - (int)sampleF;
                            float noiseMul = MathHelper.Lerp(blobNoise[i0], blobNoise[i1], t);
                            float effectiveRadius = radius * noiseMul;
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                            float edgeNoise = (float)WorldGen.genRand.NextDouble() * 1.5f;
                            inside = dist <= effectiveRadius - edgeNoise;
                            normalizedDist = dist / Math.Max(1f, effectiveRadius);
                            break;
                        }
                        default: { // Circular
                            float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                            float edgeNoise = (float)WorldGen.genRand.NextDouble() * 2f;
                            inside = dist <= radius - edgeNoise;
                            normalizedDist = dist / radius;
                            break;
                        }
                    }

                    if (!inside) continue;

                    int tileType;
                    if (normalizedDist <= coreFraction) {
                        // Core: dead Stone/Ash mix
                        tileType = WorldGen.genRand.NextBool(4) ? TileID.Ash : TileID.Stone;
                    }
                    else {
                        // Shell: vein ore Religia, makin jarang ke tepi
                        float shellPct = Math.Clamp((normalizedDist - coreFraction) / Math.Max(0.01f, 1f - coreFraction), 0f, 1f);
                        bool placeOre = WorldGen.genRand.NextFloat() > shellPct * 0.5f;
                        tileType = placeOre ? oreTileType : (WorldGen.genRand.NextBool(3) ? TileID.Ash : TileID.Stone);
                    }

                    WorldGen.PlaceTile(x, y, tileType, mute: true, forced: true);
                }
            }

            // Vein ore tambahan tersebar acak di dalam core biar ga cuma di shell.
            int veinAttempts = radius * 2;
            for (int i = 0; i < veinAttempts; i++) {
                float angle = WorldGen.genRand.NextFloat(0f, MathHelper.TwoPi);
                float dist = WorldGen.genRand.NextFloat(0f, coreRadius);
                int vx = cx + (int)(Math.Cos(angle) * dist);
                int vy = cy + (int)(Math.Sin(angle) * dist);

                if (WorldGen.InWorld(vx, vy, 5)) {
                    WorldGen.PlaceTile(vx, vy, oreTileType, mute: true, forced: true);
                }
            }
        }
    }
}