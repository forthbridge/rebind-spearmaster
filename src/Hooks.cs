using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RebindSpearmaster
{
    internal static class Hooks
    {
        public static void ApplyHooks()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        private static bool isInit = false;

        private static void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            if (isInit) return;
            isInit = true;

            MachineConnector.SetRegisteredOI(Plugin.MOD_ID, Options.instance);

            try
            {
                IL.Player.GrabUpdate += Player_GrabUpdate;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }
        }

        private static bool IsCustomKeybindPressed(Player player)
        {
            return player.playerState.playerNumber switch
            {
                0 => Input.GetKey(Options.keybindPlayer1.Value) || Input.GetKey(Options.keybindKeyboard.Value),
                1 => Input.GetKey(Options.keybindPlayer2.Value),
                2 => Input.GetKey(Options.keybindPlayer3.Value),
                3 => Input.GetKey(Options.keybindPlayer4.Value),

                _ => false
            };
        }


        private static Dictionary<Player, bool> wasInputProcessed = new Dictionary<Player, bool>();

        private static void Player_GrabUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            ILLabel extractDest = null!;
            ILLabel afterSMCheckDest = null!;

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Action<Player>>(player =>
            {
                wasInputProcessed[player] = false;
            });


            // Move closer to target
            c.GotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<Player>("PickupPressed"));

            // Get Destination
            c.GotoNext(MoveType.After,
                x => x.MatchLdloc(3),
                x => x.MatchLdcI4(-1),
                x => x.MatchBle(out extractDest));



            // Spearmaster Check
            c.GotoNext(MoveType.After,
                x => x.MatchLdarg(0),
                x => x.MatchCallOrCallvirt<Player>("get_input"),
                x => x.MatchLdcI4(0),
                x => x.MatchLdelema<Player.InputPackage>(),
                x => x.MatchLdfld<Player.InputPackage>("y"),
                x => x.MatchBrtrue(out afterSMCheckDest));

            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<Player, bool>>((player) =>
            {
                wasInputProcessed[player] = true;

                // Run depending on input
                return IsCustomKeybindPressed(player);
            });

            c.Emit(OpCodes.Brfalse, afterSMCheckDest);



            // Move just before PickupPressed checks
            c.GotoNext(MoveType.After,
                x => x.MatchStfld<Player>("wantToThrow"));

            c.Index++;

            // Branch back to check extraction
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<Player, bool>>((player) =>
            {
                return !wasInputProcessed[player];
            });

            c.Emit(OpCodes.Brtrue, extractDest);
            //c.Emit(OpCodes.Ldloc_S, (byte)6);
        }
    }
}
