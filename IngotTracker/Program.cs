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
using EmptyKeys.UserInterface.Generated.StoreBlockView_Bindings;
using Sandbox.Game.Entities.Blocks;
using System.Security.Cryptography;
using Sandbox.Engine.Multiplayer;

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


        /// <summary>
        /// Stores a dictionary that has keys that are the name of ingots / ores and values that have the multiplier for how many kg of ore gets turned into kg of ingots.
        /// </summary>
        private static readonly Dictionary<string, double> OreEfficiency = new Dictionary<string, double>()
        {
            { "Gold", 0.02 },
            { "Platinum", 0.1 },
            { "Stone", 0.014 },
            { "Iron", 1.4 },
            { "Silicon", 1.4 },
            { "Nickel", 0.8 },
            { "Cobalt", 0.6 },
            { "Silver", 0.2 },
            { "Uranium", 0.02 },
            { "Magnesium", 0.014 }
        };

        /// <summary>
        /// Struct that is used to hold how many ingots are used in a certain recipe.
        /// </summary>
        private struct Resource
        {
            public string Type;
            public double Count;

            public Resource(string type, double count)
            {
                Type = type;
                Count = count;
            }
        }

        // 1/3 normal rounded to 2 decimals.
        /// <summary>
        /// Stores a dictionary that has keys that are the name of components and values that are the recipes.
        /// </summary>
        private static readonly Dictionary<string, Recipe> Recipes = new Dictionary<string, Recipe>()
        {
            { "ThrustComponent", new Recipe("Iron:10,Cobalt:3.33,Gold:0.33,Platinum:0.13") },
            { "BulletproofGlass", new Recipe("Silicon:1.66") },
            { "Computer", new Recipe("Iron:0.06,Silicon:0.02") },
            { "ConstructionComponent", new Recipe("Iron:0.9") },
            { "DetectorComponent", new Recipe("Iron:0.56,Nickel:1.67")},
            { "Missile200mm", new Recipe("Iron:18.33,Nickel:2.33,Silicon:0.07,Uranium:0.03,Platinum:0.01,Magnesium:0.4") },
            { "ReactorComponent", new Recipe("Iron:5,Gravel:6.67,Silver:1.67") }
        };

        /// <summary>
        /// Struct that is used to help parse recipe components and allows you to ask what ingots would be needed to create a certain count of items.
        /// </summary>
        private struct Recipe
        {
            /// <summary>
            /// List of needed ingots for a single operation.
            /// </summary>
            private readonly List<Resource> Resources;

            /// <summary>
            /// Constructor that takes in a list of resources in the form of a specially formatted string.
            /// </summary>
            /// <param name="resources">Resource list in the format of "ItemName:ItemCount" seperated by commas. If any single item can't be parsed, it will just skip the item and parse the rest of the list.</param>
            public Recipe(string resources)
            {
                var resourceSplit = resources.Split(',');
                Resources = new List<Resource>();
                foreach (var resource in resourceSplit)
                {
                    var split = resource.Split(':');

                    // Check to see if the format is correct. If not, then skip this ingot.
                    if (split.Length != 2)
                        continue;
                    var item = split[0].Trim();

                    // Check to see if the item name is a valid and known ingot. If not, then skip this ingot.
                    if (!OreEfficiency.ContainsKey(item))
                        continue;
                    double count;

                    // Check to see if the count is a valid and positive double. If not, then skip this ingot.
                    if (!double.TryParse(split[1].Trim(), out count) || count <= 0)
                        continue;
                    Resources.Add(new Resource(item, count));
                }
            }

            public Dictionary<string, double> GetResourceNeeded(int count = 1)
            {
                var output = new Dictionary<string, double>();
                foreach (var resource in Resources)
                {
                    output.Add(resource.Type, resource.Count * count);
                }

                return output;
            }
        }

        /// <summary>
        /// Struct that is used to store how many ingots are needed, how many ingots we have, and how much ore we have. With that information in one place it makes it easy to figure out what is or is not missing.
        /// </summary>
        private struct Coverage
        {
            public string IngotName;
            public double IngotsNeeded;
            public double IngotsCount;
            public double OreCount;

            public Coverage(string ingotName, double ingotsNeeded, double ingotsCount, double oreCount)
            {
                IngotName = ingotName;
                IngotsNeeded = ingotsNeeded;
                IngotsCount = ingotsCount;
                OreCount = oreCount;
            }

            /// <summary>
            /// Used to format the output of any of the percent values. P tells C# its a percent, the number says how many decimals.
            /// </summary>
            private static readonly string PercentFormat = "P1";

            /// <summary>
            /// The number of missing ingots.
            /// </summary>
            public double MissingIngots => Math.Max(0, IngotsNeeded - IngotsCount);
            /// <summary>
            /// How many ingots can be made of the current amount of ore.
            /// </summary>
            public double OreToIngots => OreCount * OreEfficiency[IngotName];
            /// <summary>
            /// The number of ingots + the number of ingots can be made from the current amount of ore.
            /// </summary>
            public double TotalIngots => IngotsCount + OreToIngots;
            /// <summary>
            /// The number of missing ingots if all ore was converted to ingots.
            /// </summary>
            public double MissingTotalIngots => Math.Max(0, IngotsNeeded - TotalIngots);
            /// <summary>
            /// The number of missing ore.
            /// </summary>
            public double MissingOre => MissingTotalIngots / OreEfficiency[IngotName];

            /// <summary>
            /// What percentage of the queued items can be crafted with the current number of ingots, as a double.
            /// </summary>
            public double IngotCoverage => Math.Min(IngotsCount / IngotsNeeded, 1.0);
            /// <summary>
            /// What percentage of the queued items can be crafted with the current number of ingots, as a formatted string.
            /// </summary>
            public string IngotCoverageString => IngotCoverage.ToString(PercentFormat);
            /// <summary>
            /// What percentage of the queued items can be crafted with the current number of ingots and ore as ingots, as a double.
            /// </summary>
            public double TotalIngotCoverage => Math.Min(TotalIngots / IngotsNeeded, 1.0);
            /// <summary>
            /// What percentage of the queued items can be crafted with the current number of ingots and ore as ingots, as a formatted string.
            /// </summary>
            public string TotalIngotCoverageString => TotalIngotCoverage.ToString(PercentFormat);
        }

        private List<IMyTextSurface> MissingOreDisplays = new List<IMyTextSurface>();
        private List<IMyTextSurface> IngotCoverageDisplays = new List<IMyTextSurface>();
        private List<IMyTextSurface> TotalIngotCoverageDisplays = new List<IMyTextSurface>();
        private List<IMyTextSurface> QueuedItemsDisplays = new List<IMyTextSurface>();
        private List<IMyTextSurface> MissingRecipeDisplays = new List<IMyTextSurface>();

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            ParseArgument(Storage);
        }

        private void ParseArgument(string arg)
        {
            arg = arg.ToLower();
            // If the argument is only 'update', then we run ParseArgument with the arg stored in storage.
            if (arg.Trim() == "update")
                arg = Storage;
            // If the argument is only 'output', then we output the stored arg to the display screen attached to the programmable block and then leave.
            if(arg.Trim() == "output")
            {
                var surface = Me.GetSurface(0);
                surface.ContentType = ContentType.NONE;
                surface.WriteText(Storage);
                return;
            }
            // We check to see if the '-add' flag is true. We could do this with a simple .contains check, but I'm testing out this feature.
            MyCommandLine myCommandLine = new MyCommandLine();
            if(myCommandLine.TryParse(arg))
            {
                // If we do have the flag, then we add the current arg to the storage and run ParseArgument with the full storage arg.
                if(myCommandLine.Switch("add"))
                {
                    // We want to remove the flag so we dont keep adding every time we run update.
                    arg.Replace("-add", "");
                    Storage += ";" + arg;
                    arg = Storage;
                }
            }
            // Clearing the lists so we dont multi-add displays.
            MissingOreDisplays = new List<IMyTextSurface>();
            IngotCoverageDisplays = new List<IMyTextSurface>();
            TotalIngotCoverageDisplays = new List<IMyTextSurface>();
            QueuedItemsDisplays = new List<IMyTextSurface>();

            // We use ; as a command seperator.
            var commands = arg.Split(';');
            foreach (var commandLine in commands)
            {
                // Each command has to have an assignment.
                var commandSplit = commandLine.Split('=');
                if (commandSplit.Length != 2)
                    continue;
                var command = commandSplit[0].Trim();
                var value = commandSplit[1];
                List<IMyTerminalBlock> valueSearch = new List<IMyTerminalBlock>();
                // We search for blocks with that name.
                GridTerminalSystem.SearchBlocksOfName(value, valueSearch);
                // And we filter it because we only care about IMyTextSurface blocks (lcd screens and the like)
                var valueLcdSearch = new List<IMyTextSurface>();
                foreach (var searchBlock in valueSearch)
                {
                    // We only consider blocks on the same grid.
                    if (!searchBlock.CubeGrid.IsSameConstructAs(Me.CubeGrid))
                        continue;
                    if (searchBlock is IMyTextSurface)
                        valueLcdSearch.Add(searchBlock as IMyTextSurface);
                }
                // If we can't find any text surfaces with that name then we print an error message on the control panel screen of the programmable block.
                if (valueLcdSearch.Count == 0)
                {
                    Echo("No text surface by the name of '" + value + "'");
                    continue;
                }

                // Here is where the command names go. Putting variations of them here.
                switch (command)
                {
                    case "missing ore":
                    case "missingore":
                    case "missing":
                        MissingOreDisplays.AddList(valueLcdSearch);
                        break;
                    case "coverage":
                        IngotCoverageDisplays.AddList(valueLcdSearch);
                        break;
                    case "totalcoverage":
                    case "total coverage":
                    case "total":
                        TotalIngotCoverageDisplays.AddList(valueLcdSearch);
                        break;
                    case "queue":
                    case "queued items":
                    case "queueditems":
                        QueuedItemsDisplays.AddList(valueLcdSearch);
                        break;
                    case "missingrecipe":
                    case "unknown":
                        MissingRecipeDisplays.AddList(valueLcdSearch);
                        break;
                    default:
                        break;
                }
            }
            // Once we get to the end we store the arg. This is so we know what screens to use on startup and reduces the number of times we have to search for screens.
            Storage = arg;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // If argument has something in it and it isn't already what we already searched for.
            if (argument != Storage && !string.IsNullOrWhiteSpace(argument))
            {
                ParseArgument(argument);
            }

            // We get all existing ingots and ore in the system.
            var existingItems = GetAllExisting();
            var allIngots = existingItems[0];
            var allOres = existingItems[1];

            // We get all of the queued assembler items.
            var assemblingItems = GetTotalAssemblingItems();
            // And we calculate all of the ingots that are need to make those queued items.
            var neededIngots = GetNeededIngots(assemblingItems);

            // Then we go through and make a list of all of that data combined in the coverages.
            var coverages = new List<Coverage>();
            foreach (var neededIngotPair in neededIngots)
            {
                var ingotName = neededIngotPair.Key;
                var ingotsNeeded = neededIngotPair.Value;

                double ingotsCount;
                double oreCount;

                // We use TryGetValue here so we get the ingot/ore count or the double default vadlue of 0.
                allIngots.TryGetValue(ingotName, out ingotsCount);
                allOres.TryGetValue(ingotName, out oreCount);

                coverages.Add(new Coverage(ingotName, ingotsNeeded, ingotsCount, oreCount));
            }
            // We sort the list by ingot name so the displays don't jump around reordering stuff at random.
            coverages.Sort((c1, c2) => c1.IngotName.CompareTo(c2.IngotName));
            DisplayMissingOre(coverages);
            DisplayIngotCoverage(coverages);
            DisplayTotalIngotCoverage(coverages);
            DisplayQueuedItems(assemblingItems);
            DisplayUnknownRecipes(assemblingItems);
        }

        /// <summary>
        /// Displays the missing ores using the programmable block's 'Echo' command.
        /// </summary>
        /// <param name="coverages">The list of ingot coverages.</param>
        private void DisplayMissingOre(List<Coverage> coverages)
        {
            var output = "Missing Ore:" + Environment.NewLine;
            foreach (var coverage in coverages)
            {
                var name = coverage.IngotName;
                var missingOre = Math.Ceiling(coverage.MissingOre);
                if (missingOre != 0)
                    output += "---" + name + ": " + missingOre + "kg" + Environment.NewLine;
            }
            if (MissingOreDisplays?.Count > 0)
            {
                foreach (var display in MissingOreDisplays)
                {
                    display.ContentType = ContentType.TEXT_AND_IMAGE;
                    display.WriteText(output);
                }
                Echo("Missing ore displaying on " + MissingOreDisplays.Count + " displays.");
            }
            else
                Echo(output + Environment.NewLine);
        }

        /// <summary>
        /// Displays the ingot coverage using the programmable block's 'Echo' command.
        /// </summary>
        /// <param name="coverages">The list of ingot coverages.</param>
        private void DisplayIngotCoverage(List<Coverage> coverages)
        {
            var output = "Ingot Coverage:" + Environment.NewLine;
            foreach (var coverage in coverages)
            {
                var name = coverage.IngotName;
                var ingotCoverage = coverage.IngotCoverageString;
                output += "---" + name + ": " + ingotCoverage + Environment.NewLine;
            }
            if (IngotCoverageDisplays?.Count > 0)
            {
                foreach (var display in IngotCoverageDisplays)
                {
                    display.ContentType = ContentType.TEXT_AND_IMAGE;
                    display.WriteText(output);
                }
                Echo("Ingot coverage displaying on " + IngotCoverageDisplays.Count + " displays.");
            }
            else
                Echo(output + Environment.NewLine);
        }

        /// <summary>
        /// Displays the ingot + ore coverage using the programmable block's 'Echo' command.
        /// </summary>
        /// <param name="coverages">The list of ingot coverages.</param>
        private void DisplayTotalIngotCoverage(List<Coverage> coverages)
        {
            var output = "Ingot + Ore Coverage:" + Environment.NewLine;
            foreach (var coverage in coverages)
            {
                var name = coverage.IngotName;
                var ingotCoverage = coverage.TotalIngotCoverageString;
                output += "---" + name + ": " + ingotCoverage + Environment.NewLine;
            }
            if (TotalIngotCoverageDisplays?.Count > 0)
            {
                foreach (var display in TotalIngotCoverageDisplays)
                {
                    display.ContentType = ContentType.TEXT_AND_IMAGE;
                    display.WriteText(output);
                }
                Echo("Ingot + ore coverage displaying on " + TotalIngotCoverageDisplays.Count + " displays.");
            }
            else
                Echo(output + Environment.NewLine);
        }

        /// <summary>
        /// Displays the count of queued items using the programmable block's 'Echo' command.
        /// </summary>
        /// <param name="coverages">The list of ingot coverages.</param>
        private void DisplayQueuedItems(Dictionary<string, int> totalItems)
        {
            var output = "Queued Items:" + Environment.NewLine;
            foreach (var item in totalItems)
            {
                var name = item.Key;
                var amount = item.Value;
                output += "---" + name + ": " + amount + Environment.NewLine;
            }
            if (QueuedItemsDisplays?.Count > 0)
            {
                foreach (var display in QueuedItemsDisplays)
                {
                    display.ContentType = ContentType.TEXT_AND_IMAGE;
                    display.WriteText(output);
                }
                Echo("Queued items displaying on " + QueuedItemsDisplays.Count + " displays.");
            }
            else
                Echo(output + Environment.NewLine);
        }

        /// <summary>
        /// Displays any unknown recipes found in the assemblers using the programmable block's 'Echo' command.
        /// </summary>
        /// <param name="coverages">The list of ingot coverages.</param>
        private void DisplayUnknownRecipes(Dictionary<string, int> assemblingItems)
        {
            var output = "Unknown recipes:" + Environment.NewLine;
            foreach (var item in assemblingItems)
            {
                var itemName = item.Key;
                if (!Recipes.ContainsKey(itemName))
                    output += itemName + Environment.NewLine;
            }
            if (MissingRecipeDisplays?.Count > 0)
            {
                foreach (var display in MissingRecipeDisplays)
                {
                    display.ContentType = ContentType.TEXT_AND_IMAGE;
                    display.WriteText(output);
                }
                Echo("Missing recipes displaying on " + MissingRecipeDisplays.Count + " displays.");
            }
            else
                Echo(output + Environment.NewLine);
        }

        /// <summary>
        /// Returns a list of all of the blocks that have inventories.
        /// </summary>
        /// <returns>A list of all blocks that have inventories. Only blocks on the same grid are considered.</returns>
        private List<IMyTerminalBlock> GetAllInventoryBlocks()
        {
            var output = new List<IMyTerminalBlock>();

            GridTerminalSystem.GetBlocks(output);
            output = output.Where(block => block.InventoryCount > 0 && block.CubeGrid.IsSameConstructAs(Me.CubeGrid)).ToList();

            return output;
        }

        /// <summary>
        /// Returns 2 dictionaries of all of the ingots and ores found in the GetAllInventoryBlocks blocks.
        /// </summary>
        /// <returns>A list with dictionaries that has the total count of ingots (0) and ores (1). Keys are the SubtypeId (Iron, Gold, etc.), values are a double that is the count.</returns>
        private List<Dictionary<string, double>> GetAllExisting()
        {
            var allInventoryBlocks = GetAllInventoryBlocks();

            var output = new List<Dictionary<string, double>>();
            var ingots = new Dictionary<string, double>();
            var ores = new Dictionary<string, double>();

            foreach (var block in allInventoryBlocks)
            {
                // If we somehow got a block in here that doesn't have an inventory then skip it.
                if (block.InventoryCount == 0)
                    continue;
                // We go through all inventories in each block. Things like assemblers have an input and output inventory.
                for (int i = 0; i < block.InventoryCount; ++i)
                {
                    var curInventory = block.GetInventory(i);
                    var curItems = new List<MyInventoryItem>();
                    curInventory.GetItems(curItems);
                    foreach (var item in curItems)
                    {
                        // If item is an ingot
                        if (item.Type.TypeId == "MyObjectBuilder_Ingot")
                        {
                            var ingotName = item.Type.SubtypeId;
                            // The amount is a long with 6 decimals of precision, so we shift the decimal back to the normal position.
                            var count = item.Amount.RawValue / 1000000.0;
                            if (ingots.ContainsKey(ingotName))
                                ingots[ingotName] += count;
                            else
                                ingots.Add(ingotName, count);
                        }
                        // If item is an ore
                        if (item.Type.TypeId == "MyObjectBuilder_Ore")
                        {
                            var oreName = item.Type.SubtypeId;
                            // The amount is a long with 6 decimals of precision, so we shift the decimal back to the normal position.
                            var count = item.Amount.RawValue / 1000000.0;
                            if (ores.ContainsKey(oreName))
                                ores[oreName] += count;
                            else
                                ores.Add(oreName, count);
                        }
                    }
                }
            }
            output.Add(ingots);
            output.Add(ores);
            return output;
        }

        /// <summary>
        /// Returns a dictionary that contains all of the items that are in an assembing queue along with how many are queued.
        /// </summary>
        /// <returns>A dictionary where keys are the SubtypeId of the item being assembled, and values are the total number being queued.</returns>
        private Dictionary<string, int> GetTotalAssemblingItems()
        {
            var assemblerBlocks = new List<IMyTerminalBlock>();
            // Get all assemblers.
            GridTerminalSystem.GetBlocksOfType<IMyAssembler>(assemblerBlocks);
            var productionItems = new List<MyProductionItem>();
            foreach (var assemblerBlock in assemblerBlocks)
            {
                var assembler = assemblerBlock as IMyAssembler;
                // We only consider assemblers part of the same grid.
                if (!assembler.CubeGrid.IsSameConstructAs(Me.CubeGrid))
                    continue;
                var curQueue = new List<MyProductionItem>();
                // Get the queue then add those values to the total list.
                assembler.GetQueue(curQueue);
                productionItems.AddList(curQueue);
            }

            var totalItems = new Dictionary<string, int>();
            // Here we go through the list and coalate all of the same items into a dictionary so we can get a single number of each component being crafted.
            foreach (var productionItem in productionItems)
            {
                var blueprint = productionItem.BlueprintId.SubtypeName;
                // The amount is a long with 6 decimals of precision, so we shift the decimal back to the normal position.
                var amount = (int)(productionItem.Amount.RawValue / 1000000);
                if (totalItems.ContainsKey(blueprint))
                    totalItems[blueprint] += amount;
                else
                    totalItems.Add(blueprint, amount);
            }

            return totalItems;
        }

        /// <summary>
        /// Takes in a Dictionary of items that need to be crafted and returns a dictionary of the required ingots.
        /// </summary>
        /// <param name="items">A dictionary of items that need to be built. Keys are the SubtypeId of the items and values are the count.</param>
        /// <returns>A dictionary of ingots needed to craft the input items. Keys are the SubtypeId of the items and values are the count.</returns>
        private Dictionary<string, double> GetNeededIngots(Dictionary<string, int> items)
        {
            var totalResources = new Dictionary<string, double>();
            foreach (var item in items)
            {
                var itemName = item.Key;
                var itemCount = item.Value;
                if (!Recipes.ContainsKey(itemName))
                    continue;
                var recipe = Recipes[itemName];
                var curResources = recipe.GetResourceNeeded(itemCount);
                foreach (var resource in curResources)
                {
                    if (totalResources.ContainsKey(resource.Key))
                        totalResources[resource.Key] += resource.Value;
                    else
                        totalResources.Add(resource.Key, resource.Value);
                }
            }

            return totalResources;
        }
    }
}
