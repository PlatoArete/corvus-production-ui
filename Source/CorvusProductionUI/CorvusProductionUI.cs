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
                if (product.IsWeapon) 
                {
                    if (product.IsRangedWeapon) return "CategoryRangedWeapons".Translate();
                    else return "CategoryMeleeWeapons".Translate();
                }
                if (product.IsApparel) return "CategoryApparel".Translate();
                if (product.IsIngestible) return "CategoryFood".Translate();
                if (product.IsMedicine) return "CategoryMedicine".Translate();
                if (product.IsStuff) return "CategoryMaterials".Translate();
                if (product.building != null) return "CategoryBuildings".Translate();
                if (product.thingCategories?.Any(c => c.defName.Contains("Drug")) == true) return "CategoryDrugs".Translate();
            }
            return "CategoryOther".Translate();
        }

        private ThingDef GetWorkbenchForRecipe(RecipeDef recipe)
        {
            if (recipe.recipeUsers?.Any() == true)
            {
                return recipe.recipeUsers.First();
            }
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
            if (recipe.modContentPack == null) return "SourceUnknown".Translate();
            if (recipe.modContentPack.IsCoreMod) return "SourceVanilla".Translate();
            
            string modName = recipe.modContentPack.Name;
            if (modName.Contains("Royalty")) return "SourceRoyalty".Translate();
            if (modName.Contains("Ideology")) return "SourceIdeology".Translate();
            if (modName.Contains("Biotech")) return "SourceBiotech".Translate();
            if (modName.Contains("Anomaly")) return "SourceAnomaly".Translate();
            
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
            if (workbench == null || !(workbench is IBillGiver billGiver))
            {
                Messages.Message("MessageNoWorkbenchForBill".Translate(), MessageTypeDefOf.RejectInput);
                return;
            }
            
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
            Messages.Message("MessageBillCreated".Translate(workbench.Label), MessageTypeDefOf.TaskCompletion);
        }
    }

    public class ProductionWindow : Window
    {
        private List<RecipeInfo> allRecipes;
        private List<RecipeInfo> filteredRecipes;
        private string searchText = "";
        private AvailabilityFilter availabilityFilter = AvailabilityFilter.Available;
        private string selectedCategory = "CategoryAll".Translate();
        private string selectedMod = "SourceAll".Translate();
        private string selectedWorkstation = "WorkstationAll".Translate();
        private Vector2 scrollPosition;
        private Vector2 billScrollPosition;

        private readonly List<string> categories;
        private List<string> availableMods;
        private List<string> availableWorkstations;

        public ProductionWindow()
        {
            this.forcePause = false;
            this.draggable = true;
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            
            // Initialize categories with translations
            categories = new List<string> 
            { 
                "CategoryAll".Translate(),
                "CategoryRangedWeapons".Translate(),
                "CategoryMeleeWeapons".Translate(),
                "CategoryApparel".Translate(),
                "CategoryFood".Translate(),
                "CategoryMedicine".Translate(),
                "CategoryMaterials".Translate(),
                "CategoryBuildings".Translate(),
                "CategoryDrugs".Translate(),
                "CategoryOther".Translate()
            };
            
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
                if (recipe.researchPrerequisite != null && !recipe.researchPrerequisite.IsFinished)
                    continue;
                if (recipe.products?.Any() != true)
                    continue;

                var recipeInfo = new RecipeInfo(recipe);
                allRecipes.Add(recipeInfo);
                mods.Add(recipeInfo.modSource);
            }
            
            availableMods = new List<string> { "SourceAll".Translate() };
            availableMods.AddRange(mods.OrderBy(m => 
                m == "SourceVanilla".Translate() ? "0" : 
                m == "SourceRoyalty".Translate() ? "1" : 
                m == "SourceIdeology".Translate() ? "2" : 
                m == "SourceBiotech".Translate() ? "3" : 
                m == "SourceAnomaly".Translate() ? "4" : m));
            
            BuildWorkstationsList();
        }

        private void BuildWorkstationsList()
        {
            var workstations = new HashSet<string>();
            foreach (var recipe in allRecipes)
            {
                if (recipe.workbenchDef != null)
                {
                    string label = recipe.hasWorkbench ? 
                        "WorkstationAvailable".Translate(recipe.workbenchDef.label) :
                        "WorkstationUnavailable".Translate(recipe.workbenchDef.label);
                    workstations.Add(label);
                }
            }
            
            availableWorkstations = new List<string> { "WorkstationAll".Translate() };
            availableWorkstations.AddRange(workstations.OrderBy(w => w));
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
            if (selectedCategory != "CategoryAll".Translate() && recipeInfo.category != selectedCategory)
                return false;

            // Mod filter
            if (selectedMod != "SourceAll".Translate() && recipeInfo.modSource != selectedMod)
                return false;

            // Workstation filter
            if (selectedWorkstation != "WorkstationAll".Translate())
            {
                var workstationName = selectedWorkstation.StartsWith("✓ ") ? selectedWorkstation.Substring(2) : selectedWorkstation;
                if (recipeInfo.workbenchDef?.label != workstationName)
                    return false;
            }

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

        private void ResetAllFilters()
        {
            selectedWorkstation = "WorkstationAll".Translate();
            selectedCategory = "CategoryAll".Translate();
            selectedMod = "SourceAll".Translate();
            availabilityFilter = AvailabilityFilter.Available;
            searchText = "";
            FilterRecipes();
        }

        private string GetAvailabilityDisplayName(AvailabilityFilter filter)
        {
            switch (filter)
            {
                case AvailabilityFilter.Available:
                    return "AvailabilityAvailable".Translate();
                case AvailabilityFilter.NoMaterials:
                    return "AvailabilityNoMaterials".Translate();
                case AvailabilityFilter.NoWorkbench:
                    return "AvailabilityNoWorkbench".Translate();
                default:
                    return "AvailabilityAll".Translate();
            }
        }

        private string GetRepeatModeDisplayText(CustomRepeatMode mode, int count)
        {
            switch (mode)
            {
                case CustomRepeatMode.DoXTimes:
                    return "RepeatCount".Translate(count);
                case CustomRepeatMode.DoUntilX:
                    return "RepeatTarget".Translate(count);
                case CustomRepeatMode.DoForever:
                    return "RepeatForever".Translate();
                default:
                    return count.ToString();
            }
        }

        private string GetRepeatModeMenuText(CustomRepeatMode mode)
        {
            switch (mode)
            {
                case CustomRepeatMode.DoXTimes:
                    return "RepeatModeDoXTimes".Translate();
                case CustomRepeatMode.DoUntilX:
                    return "RepeatModeDoUntilX".Translate();
                case CustomRepeatMode.DoForever:
                    return "RepeatModeDoForever".Translate();
                default:
                    return mode.ToString();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            var rect = inRect.ContractedBy(10f);
            
            // Title
            var titleRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, $"Production Planner ({filteredRecipes.Count} recipes found)");
            
            // Filter controls with improved layout
            var filterY = titleRect.yMax + 15f;
            var labelHeight = 18f;
            var filterHeight = 28f;
            var spacing = 15f;
            
            // Calculate proportional widths for better balance
            var totalFilterWidth = rect.width - 120f; // Reserve space for reset button
            var workstationWidth = totalFilterWidth * 0.20f; // 20%
            var categoryWidth = totalFilterWidth * 0.15f;    // 15%
            var availabilityWidth = totalFilterWidth * 0.18f; // 18%
            var sourceWidth = totalFilterWidth * 0.20f;      // 20%
            var searchWidth = totalFilterWidth * 0.22f;      // 22%
            
            // Filter labels - smaller font for cleaner look
            Text.Font = GameFont.Tiny;
            
            var workstationLabelRect = new Rect(rect.x, filterY, workstationWidth, labelHeight);
            Widgets.Label(workstationLabelRect, "Workstation:");
            
            var categoryLabelRect = new Rect(workstationLabelRect.xMax + spacing, filterY, categoryWidth, labelHeight);
            Widgets.Label(categoryLabelRect, "Type:");
            
            var availabilityLabelRect = new Rect(categoryLabelRect.xMax + spacing, filterY, availabilityWidth, labelHeight);
            Widgets.Label(availabilityLabelRect, "Availability:");
            
            var modLabelRect = new Rect(availabilityLabelRect.xMax + spacing, filterY, sourceWidth, labelHeight);
            Widgets.Label(modLabelRect, "Source:");
            
            var searchLabelRect = new Rect(modLabelRect.xMax + spacing, filterY, searchWidth, labelHeight);
            Widgets.Label(searchLabelRect, "Search:");
            
            Text.Font = GameFont.Small; // Reset font
            
            // Filter controls with better spacing
            var controlsY = filterY + labelHeight + 8f;
            
            // Workstation dropdown
            var workstationRect = new Rect(rect.x, controlsY, workstationWidth, filterHeight);
            if (Widgets.ButtonText(workstationRect, selectedWorkstation))
            {
                var workstationOptions = availableWorkstations.Select(w => 
                    new FloatMenuOption(w, () => { selectedWorkstation = w; FilterRecipes(); })).ToList();
                Find.WindowStack.Add(new FloatMenu(workstationOptions));
            }
            
            // Category dropdown
            var categoryRect = new Rect(workstationRect.xMax + spacing, controlsY, categoryWidth, filterHeight);
            if (Widgets.ButtonText(categoryRect, selectedCategory))
            {
                var floatMenu = new FloatMenu(categories.Select(c => 
                    new FloatMenuOption(c, () => { selectedCategory = c; FilterRecipes(); })).ToList());
                Find.WindowStack.Add(floatMenu);
            }

            // Availability dropdown
            var availabilityRect = new Rect(categoryRect.xMax + spacing, controlsY, availabilityWidth, filterHeight);
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
            
            // Mod dropdown
            var modRect = new Rect(availabilityRect.xMax + spacing, controlsY, sourceWidth, filterHeight);
            if (Widgets.ButtonText(modRect, selectedMod))
            {
                var modOptions = availableMods.Select(m => 
                    new FloatMenuOption(m, () => { selectedMod = m; FilterRecipes(); })).ToList();
                Find.WindowStack.Add(new FloatMenu(modOptions));
            }

            // Search box
            var searchRect = new Rect(modRect.xMax + spacing, controlsY, searchWidth, filterHeight);
            var newSearchText = Widgets.TextField(searchRect, searchText);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                FilterRecipes();
            }
            
            // Reset button (aligned to the right)
            var resetRect = new Rect(searchRect.xMax + spacing, controlsY, 90f, filterHeight);
            if (Widgets.ButtonText(resetRect, "Reset".Translate()))
            {
                ResetAllFilters();
            }

            // Split the remaining area 60/40 with better spacing
            var remainingHeight = rect.height - (controlsY + filterHeight + 20f);
            var remainingY = controlsY + filterHeight + 15f;
            
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
            var itemHeight = 95f; // Increased height for ingredients and skills
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
            
            // Recipe name with info button
            var nameRect = new Rect(innerRect.x, innerRect.y, innerRect.width * 0.35f, 20f);
            Widgets.Label(nameRect, recipe.label.CapitalizeFirst());
            
            // Info button (to the right of recipe name)
            var infoButtonRect = new Rect(nameRect.xMax + 5f, innerRect.y - 2f, 24f, 24f);
            var producedThing = recipe.ProducedThingDef;
            if (producedThing != null && Widgets.InfoCardButton(infoButtonRect, producedThing))
            {
                // InfoCardButton handles opening the info card automatically
            }
            
            // Category
            var categoryRect = new Rect(infoButtonRect.xMax + 5f, innerRect.y, 90f, 20f);
            Widgets.Label(categoryRect, recipeInfo.category);
            
            // Workbench (second row)
            var workbenchRect = new Rect(innerRect.x, innerRect.y + 22f, innerRect.width * 0.5f, 18f);
            var workbenchText = recipeInfo.workbenchDef?.label ?? "Unknown";
            GUI.color = recipeInfo.hasWorkbench ? Color.green : Color.red;
            Widgets.Label(workbenchRect, workbenchText);
            GUI.color = Color.white;
            
            // Ingredients (third row)
            var ingredientsY = innerRect.y + 42f;
            var ingredientsRect = new Rect(innerRect.x, ingredientsY, innerRect.width * 0.7f, 16f);
            Text.Font = GameFont.Tiny;
            var ingredientsText = GetIngredientsText(recipe);
            GUI.color = Color.gray;
            Widgets.Label(ingredientsRect, ingredientsText);
            GUI.color = Color.white;
            
            // Skills (fourth row)
            var skillsY = innerRect.y + 60f;
            var skillsRect = new Rect(innerRect.x, skillsY, innerRect.width * 0.7f, 16f);
            var skillsText = GetSkillsText(recipe);
            GUI.color = Color.gray;
            Widgets.Label(skillsRect, skillsText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small; // Reset font
            
            // Add Bill button (right side, centered vertically)
            var addBillRect = new Rect(innerRect.xMax - 80f, innerRect.y + 25f, 75f, 30f);
            var canCreateBill = recipeInfo.CanCreateBill();
            
            if (!canCreateBill)
            {
                GUI.color = Color.gray;
            }
            
            if (Widgets.ButtonText(addBillRect, "Add Bill".Translate()) && canCreateBill)
            {
                recipeInfo.CreateBill(1, CustomRepeatMode.DoXTimes);
                Messages.Message($"Added bill: {recipe.label}", MessageTypeDefOf.PositiveEvent);
            }
            
            GUI.color = Color.white;
        }

        private string GetIngredientsText(RecipeDef recipe)
        {
            if (recipe.ingredients?.Any() != true)
                return "No ingredients required".Translate();
                
            var ingredients = new List<string>();
            foreach (var ingredient in recipe.ingredients)
            {
                var count = ingredient.GetBaseCount();
                var materialNames = ingredient.filter.AllowedThingDefs.Take(3).Select(t => t.label).ToList();
                
                if (materialNames.Count == 1)
                {
                    ingredients.Add($"{materialNames[0]} x{count}");
                }
                else
                {
                    var materialsText = materialNames.Count > 3 ? 
                        string.Join("/", materialNames.Take(2)) + "/..." : 
                        string.Join("/", materialNames);
                    ingredients.Add($"{materialsText} x{count}");
                }
            }
            
            return string.Join(", ", ingredients);
        }

        private string GetSkillsText(RecipeDef recipe)
        {
            var skills = new List<string>();
            
            if (recipe.workSkill != null)
            {
                var skillLevel = recipe.workSkillLearnFactor > 0 ? 
                    $" (learns {recipe.workSkillLearnFactor:F1}x)" : "";
                skills.Add($"{recipe.workSkill.label}{skillLevel}");
            }
            
            if (recipe.requiredGiverWorkType != null)
            {
                skills.Add($"Work: {recipe.requiredGiverWorkType.label}");
            }
            
            if (recipe.workAmount > 0)
            {
                skills.Add($"Work: {recipe.workAmount}");
            }
            
            return skills.Any() ? string.Join(", ", skills) : "No skill requirements".Translate();
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
                Widgets.Label(noBillsRect, "No bills found".Translate());
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
            Widgets.Label(workbenchRect, $"@ {workbench.Label}".Translate());
            GUI.color = Color.white;
            
            // Bill controls (right side)
            var controlsY = innerRect.y + 45f;
            
            // Minus button
            var minusRect = new Rect(innerRect.x + 10f, controlsY, 25f, 25f);
            if (Widgets.ButtonText(minusRect, "-".Translate()))
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
            if (Widgets.ButtonText(plusRect, "+".Translate()))
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
            if (Widgets.ButtonText(detailsRect, "Details".Translate()))
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
            if (!(bill is Bill_Production productionBill)) return "1".Translate();
            
            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return "RepeatForever".Translate();
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount) return "RepeatCount".Translate(productionBill.repeatCount);
            if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount) return "RepeatTarget".Translate(productionBill.targetCount);
            return "1".Translate();
        }

        private string GetBillModeText(Bill bill)
        {
            if (!(bill is Bill_Production productionBill)) return "x1".Translate();
            
            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return "RepeatForever".Translate();
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount) return "RepeatCount".Translate(productionBill.repeatCount);
            if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount) return "RepeatTarget".Translate(productionBill.targetCount);
            return "x1".Translate();
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
                new FloatMenuOption("RepeatModeDoXTimes".Translate(), () => {
                    productionBill.repeatMode = BillRepeatModeDefOf.RepeatCount;
                    if (productionBill.repeatCount <= 0) productionBill.repeatCount = 1;
                }),
                new FloatMenuOption("RepeatModeDoUntilX".Translate(), () => {
                    productionBill.repeatMode = BillRepeatModeDefOf.TargetCount;
                    if (productionBill.targetCount <= 0) productionBill.targetCount = 1;
                }),
                new FloatMenuOption("RepeatModeDoForever".Translate(), () => {
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