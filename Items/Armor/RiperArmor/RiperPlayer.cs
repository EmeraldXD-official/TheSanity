using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.NPCs;

namespace TheSanity.Players
{
	public class RiperPlayer : ModPlayer
	{
		// Set by RiperHood.UpdateArmorSet every tick the set is worn.
		// We reset it to false at the start of ResetEffects() so that if the
		// player unequips a piece mid-frame, UpdateArmorSet simply won't run
		// again and the flag will correctly read false next frame.
		public bool riperSetActive;

		// Index (whoAmI) of this player's summoned Reaper NPC, -1 if none.
		public int reaperNpcIndex = -1;

		public override void ResetEffects()
		{
			riperSetActive = false;
		}

		public override void PostUpdateEquips()
		{
			// Only the server (or single player, which counts as server) should
			// spawn/despawn NPCs - NPCs are synced automatically to clients.
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			bool reaperAlive = reaperNpcIndex >= 0
				&& reaperNpcIndex < Main.maxNPCs
				&& Main.npc[reaperNpcIndex].active
				&& Main.npc[reaperNpcIndex].type == ModContent.NPCType<Reaper>()
				&& Main.npc[reaperNpcIndex].ai[0] == Player.whoAmI; // ai[0] stores owner index, see Reaper.cs

			if (riperSetActive && Player.active && !Player.dead)
			{
				if (!reaperAlive)
				{
					SpawnReaper();
				}
			}
			else
			{
				if (reaperAlive)
				{
					DespawnReaper();
				}
			}
		}

		private void SpawnReaper()
		{
			int index = NPC.NewNPC(
				Player.GetSource_Misc("RiperArmorSetBonus"),
				(int)Player.Center.X,
				(int)Player.Center.Y,
				ModContent.NPCType<Reaper>()
			);

			if (index >= 0 && index < Main.maxNPCs)
			{
				reaperNpcIndex = index;
				Main.npc[index].ai[0] = Player.whoAmI; // remember which player owns this Reaper
				Main.npc[index].netUpdate = true;
			}
		}

		private void DespawnReaper()
		{
			if (reaperNpcIndex >= 0 && reaperNpcIndex < Main.maxNPCs)
			{
				Main.npc[reaperNpcIndex].active = false;
				Main.npc[reaperNpcIndex].netUpdate = true;
			}

			reaperNpcIndex = -1;
		}

		public override void OnRespawn()
		{
			// If the player dies, remove Reaper too; it will respawn next tick if the set is still worn
			DespawnReaper();
		}
	}
}
