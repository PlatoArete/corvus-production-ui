using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;
using System.Linq;
using System;

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
        private Vector2 billScrollPosition;

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

            // Split the remaining area 60/40
            var remainingHeight = rect.height - (filterY + filterHeight + 20f);
            var remainingY = filterY + filterHeight + 10f;
            
            // Recipe list (left 60%)
            var recipeListRect = new Rect(rect.x, remainingY, rect.width * 0.6f - 5f, remainingHeight);
            DrawRecipeList(recipeListRect);
            
            // Bill list (right 40%)
            var billListRect = new Rect(rect.x + rect.width * 0.6f + 5f, remainingY, rect.width * 0.4f - 5f, remainingHeight);
            DrawBillList(billListRect);
        }

        private void DrawRecipeList(Rect rect)
        {
            Text.Font = GameFont.Small;
            
            // Header
            var headerRect = new Rect(rect.x, rect.y, rect.width, 25f);
            Widgets.Label(headerRect, "Recipes");
            
            // List area
            var listRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            var itemHeight = 65f; // Simpler layout, less height needed
            var contentHeight = filteredRecipes.Count * itemHeight;
            var viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
            
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
            
            // Recipe name
            var nameRect = new Rect(innerRect.x, innerRect.y, innerRect.width * 0.4f, 20f);
            Widgets.Label(nameRect, recipe.label.CapitalizeFirst());
            
            // Category
            var categoryRect = new Rect(nameRect.xMax + 5f, innerRect.y, 80f, 20f);
            Widgets.Label(categoryRect, recipeInfo.category);
            
            // Workbench
            var workbenchRect = new Rect(innerRect.x, innerRect.y + 22f, innerRect.width * 0.5f, 20f);
            var workbenchText = recipeInfo.workbenchDef?.label ?? "Unknown";
            GUI.color = recipeInfo.hasWorkbench ? Color.green : Color.red;
            Widgets.Label(workbenchRect, workbenchText);
            GUI.color = Color.white;
            
            // Add Bill button
            var addBillRect = new Rect(innerRect.xMax - 80f, innerRect.y + 10f, 75f, 30f);
            var canCreateBill = recipeInfo.CanCreateBill();
            
            if (!canCreateBill)
            {
                GUI.color = Color.gray;
            }
            
            if (Widgets.ButtonText(addBillRect, "Add Bill") && canCreateBill)
            {
                recipeInfo.CreateBill(1, CustomRepeatMode.DoXTimes);
                Messages.Message($"Added bill: {recipe.label}", MessageTypeDefOf.PositiveEvent);
            }
            
            GUI.color = Color.white;
        }

        private void DrawBillList(Rect rect)
        {
            Text.Font = GameFont.Small;
            
            // Header
            var headerRect = new Rect(rect.x, rect.y, rect.width, 25f);
            Widgets.Label(headerRect, "Bills");
            
            // Get relevant bills
            var relevantBills = GetRelevantBills();
            
            // List area
            var listRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            
            if (relevantBills.Count == 0)
            {
                var noBillsRect = new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 30f);
                GUI.color = Color.gray;
                Widgets.Label(noBillsRect, "No bills found");
                GUI.color = Color.white;
                return;
            }
            
            var itemHeight = 85f;
            var contentHeight = relevantBills.Count * itemHeight;
            var viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);
            
            Widgets.BeginScrollView(listRect, ref billScrollPosition, viewRect);
            
            var curY = 0f;
            foreach (var billInfo in relevantBills)
            {
                var itemRect = new Rect(0f, curY, viewRect.width, itemHeight - 5f);
                DrawBillItem(itemRect, billInfo);
                curY += itemHeight;
            }
            
            Widgets.EndScrollView();
        }

        private List<BillInfo> GetRelevantBills()
        {
            var relevantBills = new List<BillInfo>();
            
            if (Find.CurrentMap?.listerThings == null) return relevantBills;
            
            // Get all workbenches that can make the currently filtered recipes
            var relevantWorkbenches = new HashSet<Thing>();
            foreach (var recipeInfo in filteredRecipes)
            {
                if (recipeInfo.workbenchDef != null)
                {
                    var workbenches = Find.CurrentMap.listerThings.ThingsOfDef(recipeInfo.workbenchDef);
                    foreach (var workbench in workbenches)
                    {
                        relevantWorkbenches.Add(workbench);
                    }
                }
            }
            
            // Get bills from relevant workbenches, interleaved by position
            var workbenchBills = new Dictionary<Thing, List<Bill>>();
            int maxBillCount = 0;
            
            foreach (var workbench in relevantWorkbenches)
            {
                if (workbench is IBillGiver billGiver)
                {
                    var bills = billGiver.BillStack.Bills
                        .Where(b => filteredRecipes.Any(r => r.recipe == b.recipe))
                        .ToList();
                    workbenchBills[workbench] = bills;
                    maxBillCount = Math.Max(maxBillCount, bills.Count);
                }
            }
            
            // Interleave bills by position
            for (int i = 0; i < maxBillCount; i++)
            {
                foreach (var kvp in workbenchBills)
                {
                    if (i < kvp.Value.Count)
                    {
                        relevantBills.Add(new BillInfo(kvp.Value[i], kvp.Key));
                    }
                }
            }
            
            return relevantBills;
        }

        private void DrawBillItem(Rect rect, BillInfo billInfo)
        {
            // Background
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }
            Widgets.DrawBox(rect);

            var bill = billInfo.bill;
            var workbench = billInfo.workbench;
            var innerRect = rect.ContractedBy(5f);
            
            // Bill name and workbench info
            var nameRect = new Rect(innerRect.x, innerRect.y, innerRect.width * 0.6f, 20f);
            Widgets.Label(nameRect, bill.LabelCap);
            
            var workbenchRect = new Rect(innerRect.x, innerRect.y + 22f, innerRect.width * 0.6f, 20f);
            GUI.color = Color.gray;
            Widgets.Label(workbenchRect, $"@ {workbench.Label}");
            GUI.color = Color.white;
            
            // Bill controls (right side)
            var controlsY = innerRect.y + 45f;
            
            // Minus button
            var minusRect = new Rect(innerRect.x + 10f, controlsY, 25f, 25f);
            if (Widgets.ButtonText(minusRect, "-"))
            {
                ModifyBillCount(bill, -1);
            }
            
            // Count display/edit
            var countRect = new Rect(minusRect.xMax + 5f, controlsY, 50f, 25f);
            string countText = GetBillCountText(bill);
            var newCountText = Widgets.TextField(countRect, countText);
            if (newCountText != countText && int.TryParse(newCountText, out int newCount) && newCount > 0)
            {
                SetBillCount(bill, newCount);
            }
            
            // Plus button
            var plusRect = new Rect(countRect.xMax + 5f, controlsY, 25f, 25f);
            if (Widgets.ButtonText(plusRect, "+"))
            {
                ModifyBillCount(bill, 1);
            }
            
            // Repeat mode button
            var modeRect = new Rect(plusRect.xMax + 10f, controlsY, 50f, 25f);
            var modeText = GetBillModeText(bill);
            if (Widgets.ButtonText(modeRect, modeText))
            {
                ShowBillModeMenu(bill);
            }
            
            // Details button
            var detailsRect = new Rect(modeRect.xMax + 10f, controlsY, 60f, 25f);
            if (Widgets.ButtonText(detailsRect, "Details"))
            {
                if (bill is Bill_Production productionBill)
                {
                    Find.WindowStack.Add(new Dialog_BillConfig(productionBill, workbench.Position));
                }
            }
            
            // Delete button (trash icon)
            var deleteRect = new Rect(innerRect.xMax - 25f, innerRect.y + 5f, 20f, 20f);
            if (Widgets.ButtonImage(deleteRect, TexButton.Delete))
            {
                if (workbench is IBillGiver billGiver)
                {
                    billGiver.BillStack.Delete(bill);
                    Messages.Message($"Deleted bill: {bill.LabelCap}", MessageTypeDefOf.NeutralEvent);
                }
            }
        }

        private string GetBillCountText(Bill bill)
        {
            if (!(bill is Bill_Production productionBill)) return "1";
            
            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return "∞";
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount) return productionBill.repeatCount.ToString();
            if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount) return productionBill.targetCount.ToString();
            return "1";
        }

        private string GetBillModeText(Bill bill)
        {
            if (!(bill is Bill_Production productionBill)) return "x1";
            
            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return "∞";
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount) return $"x{productionBill.repeatCount}";
            if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount) return $"≤{productionBill.targetCount}";
            return "x1";
        }

        private void ModifyBillCount(Bill bill, int delta)
        {
            if (!(bill is Bill_Production productionBill)) return;
            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return;
            
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount)
            {
                productionBill.repeatCount = Math.Max(1, productionBill.repeatCount + delta);
            }
            else if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                productionBill.targetCount = Math.Max(1, productionBill.targetCount + delta);
            }
        }

        private void SetBillCount(Bill bill, int count)
        {
            if (!(bill is Bill_Production productionBill)) return;
            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return;
            
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount)
            {
                productionBill.repeatCount = count;
            }
            else if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount)
            {
                productionBill.targetCount = count;
            }
        }

        private void ShowBillModeMenu(Bill bill)
        {
            if (!(bill is Bill_Production productionBill)) return;
            
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("Do X times", () => {
                    productionBill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    if (productionBill.repeatCount <= 0) productionBill.repeatCount = 1;
                }),
                new FloatMenuOption("Do until you have X", () => {
                    productionBill.repeatMode = BillRepeatModeDefOf.TargetCount;
                    if (productionBill.targetCount <= 0) productionBill.targetCount = 1;
                }),
                new FloatMenuOption("Do forever", () => {
                    productionBill.repeatMode = BillRepeatModeDefOf.Forever;
                })
            };
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public class BillInfo
    {
        public Bill bill;
        public Thing workbench;

        public BillInfo(Bill bill, Thing workbench)
        {
            this.bill = bill;
            this.workbench = workbench;
        }
    }
} 