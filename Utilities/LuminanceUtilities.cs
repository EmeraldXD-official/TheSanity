using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

public enum ParticleType {
    Spark
}

public static class LuminanceUtilities
{
    // Minimal fallback implementation so the mod compiles when the Luminance
    // library isn't available. Spawns simple dust particles.
    public static void SpawnParticle(Vector2 position, Vector2 velocity, Color color, int count = 1, float scale = 1f, ParticleType type = ParticleType.Spark)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 v = velocity + Main.rand.NextVector2Circular(0.8f, 0.8f);
            int dustType = DustID.PurpleCrystalShard;
            int idx = Dust.NewDust(new Vector2(position.X - 2f, position.Y - 2f), 4, 4, dustType, v.X, v.Y, 150, color, scale);
            if (idx >= 0 && idx < Main.maxDust)
            {
                Dust d = Main.dust[idx];
                d.noGravity = true;
                d.velocity = v * 0.6f;
            }
        }
    }
}
