using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // Antrian spawn buat /testsoul. Kalau count-nya gede banget (ribuan), spawn
    // SEKALIGUS dalam 1 tick bisa nabrak limit slot Main.item (default ~400 slot).
    // Item.NewItem bakal gagal / numpuk/overwrite slot lama secara diam-diam kalau
    // slotnya penuh -> keliatannya kayak "soul yang masuk cuma dikit" padahal
    // sebenernya banyak yang emang GA PERNAH ke-spawn beneran.
    //
    // Fix: trickle spawn beberapa per tick aja (SpawnsPerTick), sisanya nunggu di
    // antrian. Item yang udah kekonsumsi altar bakal ninggalin slot kosong, jadi
    // batch berikutnya kebagian tempat lagi. Ga ada limit jumlah di command-nya,
    // cuma laju spawn-nya aja yang diatur.
    public class SoulTestSpawnSystem : ModSystem
    {
        private const int SpawnsPerTick = 40;

        private static readonly Queue<Vector2> _pending = new Queue<Vector2>();

        public static int PendingCount => _pending.Count;

        public static void Enqueue(Vector2 basePos, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = new Vector2(
                    Main.rand.Next(-96, 97),
                    Main.rand.Next(-64, 65));

                _pending.Enqueue(basePos + offset);
            }
        }

        public override void PostUpdateEverything()
        {
            // Cuma sisi otoritatif yang boleh spawn item (biar ga dobel di multiplayer client).
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            if (_pending.Count == 0)
                return;

            int soulType = ModContent.ItemType<Souls>();
            int spawned = 0;

            while (spawned < SpawnsPerTick && _pending.Count > 0)
            {
                Vector2 pos = _pending.Dequeue();
                Item.NewItem(new EntitySource_Misc("SoulTestCommand"), pos, 16, 16, soulType, 1);
                spawned++;
            }
        }

        public override void OnWorldUnload()
        {
            _pending.Clear();
        }
    }
}
