using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;

namespace CorvusProductionUI
{
    public class CorvusProductionMod : Mod
    {
        public CorvusProductionMod(ModContentPack content) : base(content)
        {
        }
    }

    public class MainButtonWorker_ProductionUI : MainButtonWorker
    {
        public override void Activate()
        {
            Find.WindowStack.Add(new ProductionWindow());
        }
    }

    public enum AvailabilityFilter
    {
        Available,
        NoMaterials,
        NoWorkbench
    }

    public enum CustomRepeatMode
    {
        DoXTimes,
        DoUntilX,
        DoForever
    }

    public class RecipeInfo
    {
        public RecipeDef recipe;
        public string category;
        public bool hasWorkbench;
        public bool hasMaterials;
        public ThingDef workbenchDef;
        public string modSource;

        public RecipeInfo(RecipeDef recipe)
        {
            this.recipe = recipe;
            this.category = GetRecipeCategory(recipe);
            this.workbenchDef = GetWorkbenchForRecipe(recipe);
            this.hasWorkbench = HasWorkbench();
            this.hasMaterials = HasMaterials();
            this.modSource = GetModSource(recipe);
        }

        private string GetRecipeCategory(RecipeDef recipe)
        {
            if (recipe.products?.Any() == true)
            {
                var product = recipe.products.First().thingDef;
                if (product.IsWeapon) return "Weapons";
                if (product.IsApparel) return "Apparel";
                if (product.IsIngestible) return "Food";
                if (product.IsMedicine) return "Medicine";
                if (product.IsStuff) return "Materials";
                if (product.building != null) return "Buildings";
                if (product.thingCategories?.Any(c => c.defName.Contains("Drug")) == true) return "Drugs";
            }
            return "Other";
        }

        private ThingDef GetWorkbenchForRecipe(RecipeDef recipe)
        {
            // First try recipeUsers (most common way recipes are linked to workbenches)
            if (recipe.recipeUsers?.Any() == true)
            {
                return recipe.recipeUsers.First();
            }
            
            // Fallback: look for workbenches that list this recipe
            return DefDatabase<ThingDef>.AllDefs.FirstOrDefault(t => 
                t.recipes?.Contains(recipe) == true);
        }

        private bool HasWorkbench()
        {
            if (workbenchDef == null) return false;
            return Find.CurrentMap?.listerThings?.ThingsOfDef(workbenchDef)?.Any() == true;
        }

        private bool HasMaterials()
        {
            if (recipe.ingredients == null) return true;
            
            foreach (var ingredient in recipe.ingredients)
            {
                var availableCount = 0;
                foreach (var filter in ingredient.filter.AllowedThingDefs)
                {
                    availableCount += Find.CurrentMap?.resourceCounter?.GetCount(filter) ?? 0;
                }
                if (availableCount < ingredient.GetBaseCount()) return false;
            }
            return true;
        }

        private string GetModSource(RecipeDef recipe)
        {
            if (recipe.modContentPack == null) return "Unknown";
            
            // Handle core game content
            if (recipe.modContentPack.IsCoreMod) return "Vanilla";
            
            // Handle official DLCs
            string modName = recipe.modContentPack.Name;
            if (modName.Contains("Royalty")) return "Royalty";
            if (modName.Contains("Ideology")) return "Ideology";
            if (modName.Contains("Biotech")) return "Biotech";
            if (modName.Contains("Anomaly")) return "Anomaly";
            
            // Return the mod name for other mods
            return modName;
        }

        public Thing GetBestWorkbench()
        {
            if (workbenchDef == null) return null;
            
            var workbenches = Find.CurrentMap?.listerThings?.ThingsOfDef(workbenchDef)?.Cast<Thing>().ToList();
            if (workbenches?.Any() != true) return null;
            
            // Find workbench with fewest bills
            Thing bestWorkbench = null;
            int fewestBills = int.MaxValue;
            
            foreach (var workbench in workbenches)
            {
                if (workbench is IBillGiver billGiver)
                {
                    int billCount = billGiver.BillStack.Count;
                    if (billCount < fewestBills)
                    {
                        fewestBills = billCount;
                        bestWorkbench = workbench;
                    }
                }
            }
            
            return bestWorkbench;
        }

        public bool CanCreateBill()
        {
            return GetBestWorkbench() != null;
        }

        public void CreateBill(int count = 1, CustomRepeatMode repeatMode = CustomRepeatMode.DoXTimes)
        {
            var workbench = GetBestWorkbench();
            if (workbench == null || !(workbench is IBillGiver billGiver)) return;
            
            var bill = new Bill_Production(recipe);
            bill.SetStoreMode(BillStoreModeDefOf.BestStockpile);
            
            switch (repeatMode)
            {
                case CustomRepeatMode.DoXTimes:
                    bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    bill.repeatCount = count;
                    break;
                case CustomRepeatMode.DoUntilX:
                    bill.repeatMode = BillRepeatModeDefOf.TargetCount;
                    bill.targetCount = count;
                    break;
                case CustomRepeatMode.DoForever:
                    bill.repeatMode = BillRepeatModeDefOf.Forever;
                    break;
            }
            
            billGiver.BillStack.AddBill(bill);
        }
    }

    public class ProductionWindow : Window
    {
        private List<RecipeInfo> allRecipes;
        private List<RecipeInfo> filteredRecipes;
        private string searchText = "";
        private AvailabilityFilter availabilityFilter = AvailabilityFilter.Available;
        private string selectedCategory = "All";
        private string selectedMod = "All";
        private Vector2 scrollPosition;

        private readonly List<string> categories = new List<string> 
        { 
            "All", "Weapons", "Apparel", "Food", "Medicine", "Materials", "Buildings", "Drugs", "Other" 
        };

        private List<string> availableMods = new List<string>();
        private Dictionary<RecipeInfo, int> billCounts = new Dictionary<RecipeInfo, int>();
        private Dictionary<RecipeInfo, CustomRepeatMode> repeatModes = new Dictionary<RecipeInfo, CustomRepeatMode>();

        public ProductionWindow()
        {
            this.forcePause = false;
            this.draggable = true;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            
            LoadRecipes();
            FilterRecipes();
        }

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        private void LoadRecipes()
        {
            allRecipes = new List<RecipeInfo>();
            var mods = new HashSet<string>();
            
            foreach (var recipe in DefDatabase<RecipeDef>.AllDefs)
            {
                // Only include recipes that are available based on research
                if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
                    continue;
                    
                // Skip recipes without products
                if (recipe.products?.Any() != true)
                    continue;

                var recipeInfo = new RecipeInfo(recipe);
                allRecipes.Add(recipeInfo);
                mods.Add(recipeInfo.modSource);
            }
            
            // Build available mods list
            availableMods = new List<string> { "All" };
            availableMods.AddRange(mods.OrderBy(m => m == "Vanilla" ? "0" : m == "Royalty" ? "1" : m == "Ideology" ? "2" : m == "Biotech" ? "3" : m == "Anomaly" ? "4" : m));
        }

        private void FilterRecipes()
        {
            filteredRecipes = allRecipes.Where(r => PassesFilters(r)).ToList();
        }

        private bool PassesFilters(RecipeInfo recipeInfo)
        {
            // Search filter
            if (!string.IsNullOrEmpty(searchText) && 
                !recipeInfo.recipe.label.ToLower().Contains(searchText.ToLower()))
                return false;

            // Category filter
            if (selectedCategory != "All" && recipeInfo.category != selectedCategory)
                return false;

            // Mod filter
            if (selectedMod != "All" && recipeInfo.modSource != selectedMod)
                return false;

            // Availability filter
            switch (availabilityFilter)
            {
                case AvailabilityFilter.Available:
                    return recipeInfo.hasWorkbench && recipeInfo.hasMaterials;
                case AvailabilityFilter.NoMaterials:
                    return recipeInfo.hasWorkbench && !recipeInfo.hasMaterials;
                case AvailabilityFilter.NoWorkbench:
                    return !recipeInfo.hasWorkbench;
            }

            return true;
        }

        private string GetAvailabilityDisplayName(AvailabilityFilter filter)
        {
            switch (filter)
            {
                case AvailabilityFilter.Available:
                    return "Available";
                case AvailabilityFilter.NoMaterials:
                    return "No Materials";
                case AvailabilityFilter.NoWorkbench:
                    return "No Workbench";
                default:
                    return filter.ToString();
            }
        }

        private string GetRepeatModeDisplayText(CustomRepeatMode mode, int count)
        {
            switch (mode)
            {
                case CustomRepeatMode.DoXTimes:
                    return $"x{count}";
                case CustomRepeatMode.DoUntilX:
                    return $"≤{count}";
                case CustomRepeatMode.DoForever:
                    return "∞";
                default:
                    return $"x{count}";
            }
        }

        private string GetRepeatModeMenuText(CustomRepeatMode mode)
        {
            switch (mode)
            {
                case CustomRepeatMode.DoXTimes:
                    return "Do X times";
                case CustomRepeatMode.DoUntilX:
                    return "Do until you have X";
                case CustomRepeatMode.DoForever:
                    return "Do forever";
                default:
                    return "Do X times";
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var rect = inRect.ContractedBy(10f);
            
            // Title
            var titleRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, $"Production Planner ({filteredRecipes.Count} recipes found)");
            
            // Filter controls
            var filterY = titleRect.yMax + 10f;
            var filterHeight = 30f;
            
            // Mod dropdown
            var modRect = new Rect(rect.x, filterY, 200f, filterHeight);
            if (Widgets.ButtonText(modRect, selectedMod))
            {
                var modOptions = availableMods.Select(m => 
                    new FloatMenuOption(m, () => { selectedMod = m; FilterRecipes(); })).ToList();
                Find.WindowStack.Add(new FloatMenu(modOptions));
            }
            
            // Category dropdown
            var categoryRect = new Rect(modRect.xMax + 10f, filterY, 150f, filterHeight);
            if (Widgets.ButtonText(categoryRect, selectedCategory))
            {
                var floatMenu = new FloatMenu(categories.Select(c => 
                    new FloatMenuOption(c, () => { selectedCategory = c; FilterRecipes(); })).ToList());
                Find.WindowStack.Add(floatMenu);
            }

            // Availability dropdown
            var availabilityRect = new Rect(categoryRect.xMax + 10f, filterY, 150f, filterHeight);
            if (Widgets.ButtonText(availabilityRect, GetAvailabilityDisplayName(availabilityFilter)))
            {
                var options = new List<FloatMenuOption>();
                foreach (AvailabilityFilter filter in System.Enum.GetValues(typeof(AvailabilityFilter)))
                {
                    options.Add(new FloatMenuOption(GetAvailabilityDisplayName(filter), () => 
                    { 
                        availabilityFilter = filter; 
                        FilterRecipes(); 
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // Search box
            var searchRect = new Rect(availabilityRect.xMax + 10f, filterY, 180f, filterHeight);
            var newSearchText = Widgets.TextField(searchRect, searchText);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                FilterRecipes();
            }

            // Recipe list
            var listRect = new Rect(rect.x, filterY + filterHeight + 10f, rect.width, 
                rect.height - (filterY + filterHeight + 20f));
            
            DrawRecipeList(listRect);
        }

        private void DrawRecipeList(Rect rect)
        {
            Text.Font = GameFont.Small;
            
            var itemHeight = 85f; // Increased height for two-line layout
            var contentHeight = filteredRecipes.Count * itemHeight;
            var viewRect = new Rect(0f, 0f, rect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            
            var curY = 0f;
            foreach (var recipeInfo in filteredRecipes)
            {
                var itemRect = new Rect(0f, curY, viewRect.width, itemHeight - 5f);
                DrawRecipeItem(itemRect, recipeInfo);
                curY += itemHeight;
            }
            
            Widgets.EndScrollView();
        }

        private void DrawRecipeItem(Rect rect, RecipeInfo recipeInfo)
        {
            // Background
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            Widgets.DrawBox(rect);

            var recipe = recipeInfo.recipe;
            var innerRect = rect.ContractedBy(5f);
            
            // First line: Recipe info
            var firstLineY = innerRect.y;
            
            // Recipe name
            var nameRect = new Rect(innerRect.x, firstLineY, innerRect.width * 0.3f, 20f);
            Widgets.Label(nameRect, recipe.label.CapitalizeFirst());
            
            // Category
            var categoryRect = new Rect(nameRect.xMax + 5f, firstLineY, 80f, 20f);
            Widgets.Label(categoryRect, recipeInfo.category);
            
            // Workbench
            var workbenchRect = new Rect(categoryRect.xMax + 5f, firstLineY, 120f, 20f);
            var workbenchText = recipeInfo.workbenchDef?.label ?? "Unknown";
            GUI.color = recipeInfo.hasWorkbench ? Color.green : Color.red;
            Widgets.Label(workbenchRect, workbenchText);
            GUI.color = Color.white;
            
            // Materials
            var materialsText = "Materials: ";
            if (recipe.ingredients?.Any() == true)
            {
                materialsText += string.Join(", ", recipe.ingredients.Select(ing => 
                    $"{ing.filter.Summary} x{ing.GetBaseCount()}"));
            }
            else
            {
                materialsText += "None";
            }
            
            var materialsRect = new Rect(innerRect.x, firstLineY + 22f, innerRect.width, 20f);
            GUI.color = recipeInfo.hasMaterials ? Color.green : Color.red;
            Widgets.Label(materialsRect, materialsText);
            GUI.color = Color.white;
            
            // Second line: Bill controls
            var secondLineY = innerRect.y + 45f;
            var canCreateBill = recipeInfo.CanCreateBill();
            
            if (!canCreateBill)
            {
                GUI.color = Color.gray;
            }
            
            // Get current bill count and repeat mode for this recipe
            if (!billCounts.ContainsKey(recipeInfo))
            {
                billCounts[recipeInfo] = 1;
            }
            if (!repeatModes.ContainsKey(recipeInfo))
            {
                repeatModes[recipeInfo] = CustomRepeatMode.DoXTimes;
            }
            var currentCount = billCounts[recipeInfo];
            var currentRepeatMode = repeatModes[recipeInfo];
            
            // Minus button
            var minusRect = new Rect(innerRect.x + 20f, secondLineY, 25f, 25f);
            if (Widgets.ButtonText(minusRect, "-") && canCreateBill && currentCount > 1)
            {
                billCounts[recipeInfo] = currentCount - 1;
            }
            
            // Count text field (disabled for Do Forever mode)
            var countRect = new Rect(minusRect.xMax + 5f, secondLineY, 60f, 25f);
            if (currentRepeatMode == CustomRepeatMode.DoForever)
            {
                GUI.color = Color.gray;
                Widgets.Label(countRect, "∞");
                GUI.color = canCreateBill ? Color.white : Color.gray;
            }
            else
            {
                var countString = currentCount.ToString();
                var newCountString = Widgets.TextField(countRect, countString);
                if (int.TryParse(newCountString, out int newCount) && newCount > 0)
                {
                    billCounts[recipeInfo] = newCount;
                }
            }
            
            // Plus button
            var plusRect = new Rect(countRect.xMax + 5f, secondLineY, 25f, 25f);
            if (Widgets.ButtonText(plusRect, "+") && canCreateBill && currentRepeatMode != CustomRepeatMode.DoForever)
            {
                billCounts[recipeInfo] = currentCount + 1;
            }
            
            // Repeat mode dropdown
            var repeatModeRect = new Rect(plusRect.xMax + 10f, secondLineY, 50f, 25f);
            var displayText = GetRepeatModeDisplayText(currentRepeatMode, currentCount);
            if (Widgets.ButtonText(repeatModeRect, displayText) && canCreateBill)
            {
                var options = new List<FloatMenuOption>();
                foreach (CustomRepeatMode mode in System.Enum.GetValues(typeof(CustomRepeatMode)))
                {
                    options.Add(new FloatMenuOption(GetRepeatModeMenuText(mode), () => 
                    { 
                        repeatModes[recipeInfo] = mode; 
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // Add Bill button
            var addBillRect = new Rect(repeatModeRect.xMax + 10f, secondLineY, 80f, 25f);
            if (Widgets.ButtonText(addBillRect, "Add Bill") && canCreateBill)
            {
                recipeInfo.CreateBill(billCounts[recipeInfo], currentRepeatMode);
                string modeText = currentRepeatMode == CustomRepeatMode.DoForever ? "forever" : 
                                 currentRepeatMode == CustomRepeatMode.DoUntilX ? $"until {currentCount}" : 
                                 $"{currentCount} times";
                Messages.Message($"Added bill: {recipe.label} ({modeText})", MessageTypeDefOf.PositiveEvent);
            }
            
            // Details button (placeholder)
            var detailsRect = new Rect(addBillRect.xMax + 10f, secondLineY, 80f, 25f);
            if (Widgets.ButtonText(detailsRect, "Details..."))
            {
                // Placeholder - no functionality yet
            }
            
            GUI.color = Color.white;
        }
    }
} 