using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // Chat command buat spawn Souls langsung ke dunia (bypass inventory sepenuhnya),
    // jadi ga kesenggol SoulsAntiCheatPlayer dan bisa langsung liat pull/consume-nya
    // kerja tanpa harus nunggu drop 43% dari mob.
    //
    // Usage: /testsoul [jumlah]   (default 10 kalau argumen kosong, TAK ADA LIMIT jumlah)
    // Item-nya ga langsung disemburin semua sekaligus -> masuk antrian SoulTestSpawnSystem
    // yang nge-trickle beberapa per tick, biar ga nabrak limit slot Main.item bawaan
    // Terraria kalau jumlahnya gede (lihat komentar di SoulTestSpawnSystem.cs).
    public class SoulTestCommand : ModCommand
    {
        public override string Command => "testsoul";
        public override string Usage => "/testsoul [count]";
        public override string Description => "Queues test Souls to spawn scattered around you (unlimited count, trickled in gradually).";
        public override CommandType Type => CommandType.Chat;

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            int count = 10;
            if (args.Length > 0 && int.TryParse(args[0], out int parsed))
                count = (int)MathHelper.Max(parsed, 1);

            Player player = caller.Player;
            SoulTestSpawnSystem.Enqueue(player.position, count);

            caller.Reply($"[Soul Collector] Queued {count} test Souls (spawning gradually near you).", Color.Cyan);
        }
    }
}
