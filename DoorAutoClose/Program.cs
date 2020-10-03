using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        private Dictionary<IMyDoor, double> DoorTimes = new Dictionary<IMyDoor, double>();
        private double WaitTime = 4;

        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            // Run the script every 1/10th of a second.
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
            // If we have a new wait time stored then we get it.
            ParseTimeFromStorage();
            // And we get all of the doors.
            GetAllDoors();
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.
            double newTime;
            // Check to see if the arguement is a double and non negative.
            if (double.TryParse(argument, out newTime) && newTime >= 0)
            {
                // If it is store it and parse it.
                Storage = argument;
                ParseTimeFromStorage();
            }

            // If the update is sent from someone pushing the run button or some other block triggering it, then we update the doors that are watched.
            // This allows a timer or player to update doors on some other pattern to prevent searching the grid too often.
            if (updateSource == UpdateType.Terminal || updateSource == UpdateType.Trigger)
            {
                GetAllDoors();
            }
            // We only want to count the time from 1/10th second ticks.
            else if (updateSource == UpdateType.Update10)
            {
                foreach (var door in DoorTimes.Keys)
                {
                    var isDoorOpen = door.Status == DoorStatus.Open;
                    // If the door is open then we increment the stored time and check against the wait time.
                    if (isDoorOpen)
                    {
                        DoorTimes[door] += 0.1;
                        if (DoorTimes[door] >= WaitTime)
                            door.CloseDoor();
                    }
                    // Else we set the time to 0.
                    else
                    {
                        DoorTimes[door] = 0;
                    }
                }
            }
            PrintDoorTimes();
        }

        private void PrintDoorTimes()
        {
            var openDoors = new List<string>();
            var closedDoors = new List<string>();
            foreach (var doorPair in DoorTimes)
            {
                var door = doorPair.Key;
                var time = doorPair.Value;
                var isDoorOpen = door.Status == DoorStatus.Open;

                if (isDoorOpen)
                {
                    openDoors.Add("---'" + door.CustomName + "' has been open for " + time.ToString("F1") + " seconds");
                }
                else
                {
                    closedDoors.Add("---'" + door.CustomName + "'");
                }
            }
            openDoors.Sort();
            closedDoors.Sort();

            var output = "Open Doors:\n" + string.Join("\n", openDoors);
            output += "\n\n";
            output += "Closed Doors:\n" + string.Join("\n", closedDoors);
            Echo(output);
        }

        private void ParseTimeFromStorage()
        {
            // Storage is checked for being valid before its stored, so I just parse here.
            if (string.IsNullOrWhiteSpace(Storage))
                return;
            var newTime = double.Parse(Storage);
            WaitTime = newTime;
        }

        private void GetAllDoors()
        {
            var allDoorBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyDoor>(allDoorBlocks);
            allDoorBlocks = allDoorBlocks.Where(door => door.CubeGrid.IsSameConstructAs(Me.CubeGrid)).ToList();
            var allDoors = new List<IMyDoor>();
            foreach (var block in allDoorBlocks)
            {
                if (block is IMyDoor)
                {
                    var door = block as IMyDoor;
                    allDoors.Add(door);
                    if (!DoorTimes.ContainsKey(door))
                        DoorTimes.Add(door, 0);
                }
            }

            // Remove any doors that are being watched that arent blocks anymore.
            foreach (var door in DoorTimes.Keys)
            {
                if (!allDoors.Contains(door))
                    DoorTimes.Remove(door);
            }
        }
    }
}
