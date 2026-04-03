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
        public ThingDef primaryProductDef;

        public RecipeInfo(RecipeDef recipe)
        {
            this.recipe = recipe;
            this.primaryProductDef = GetPrimaryProductDef(recipe);
            this.category = GetRecipeCategory(recipe);
            this.workbenchDef = GetWorkbenchForRecipe(recipe);
            this.hasWorkbench = HasWorkbench();
            this.hasMaterials = HasMaterials();
            this.modSource = GetModSource(recipe);
        }

        private string GetRecipeCategory(RecipeDef recipe)
        {
            var product = GetPrimaryProductDef(recipe);
            if (product != null)
            {
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

        private ThingDef GetPrimaryProductDef(RecipeDef recipe)
        {
            if (recipe == null)
            {
                return null;
            }

            if (recipe.ProducedThingDef != null)
            {
                return recipe.ProducedThingDef;
            }

            return recipe.products?
                .Select(product => product?.thingDef)
                .FirstOrDefault(thingDef => thingDef != null);
        }

        private bool IsBillGiverDef(ThingDef thingDef)
        {
            return thingDef?.thingClass != null && typeof(IBillGiver).IsAssignableFrom(thingDef.thingClass);
        }

        private ThingDef GetWorkbenchForRecipe(RecipeDef recipe)
        {
            var directWorkbench = recipe.AllRecipeUsers?.FirstOrDefault(IsBillGiverDef);
            if (directWorkbench != null)
            {
                return directWorkbench;
            }

            return DefDatabase<ThingDef>.AllDefs.FirstOrDefault(t =>
                IsBillGiverDef(t) && t.recipes?.Contains(recipe) == true);
        }

        private bool HasWorkbench()
        {
            if (workbenchDef == null) return false;
            return Find.CurrentMap?.listerThings?.ThingsOfDef(workbenchDef)?
                .Any(thing => thing is IBillGiver billGiver && billGiver.CurrentlyUsableForBills()) == true;
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
                if (workbench is IBillGiver billGiver && billGiver.CurrentlyUsableForBills())
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
        private readonly Dictionary<string, float> hoverStates = new Dictionary<string, float>();

        public ProductionWindow()
        {
            this.forcePause = false;
            this.draggable = true;
            this.doCloseX = false;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            this.doWindowBackground = false;
            this.drawShadow = false;
            
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

        public override Vector2 InitialSize
        {
            get
            {
                float maxWidth = Mathf.Max(900f, Verse.UI.screenWidth - 40f);
                float maxHeight = Mathf.Max(620f, Verse.UI.screenHeight - 70f);

                float targetWidth = Mathf.Clamp(Verse.UI.screenWidth * 0.88f, 1000f, 1180f);
                float targetHeight = Mathf.Clamp(Verse.UI.screenHeight * 0.82f, 700f, 820f);

                return new Vector2(
                    Mathf.Min(targetWidth, maxWidth),
                    Mathf.Min(targetHeight, maxHeight));
            }
        }

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
                    return recipeInfo.CanCreateBill() && recipeInfo.hasMaterials;
                case AvailabilityFilter.NoMaterials:
                    return recipeInfo.CanCreateBill() && !recipeInfo.hasMaterials;
                case AvailabilityFilter.NoWorkbench:
                    return !recipeInfo.CanCreateBill();
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

        private int GetActiveFilterCount()
        {
            int count = 0;
            if (!string.IsNullOrEmpty(searchText)) count++;
            if (selectedCategory != "CategoryAll".Translate()) count++;
            if (selectedMod != "SourceAll".Translate()) count++;
            if (selectedWorkstation != "WorkstationAll".Translate()) count++;
            if (availabilityFilter != AvailabilityFilter.Available) count++;
            return count;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            var oldWordWrap = Text.WordWrap;
            var oldColor = GUI.color;

            try
            {
            CorvusStyle.DrawWindowBackground(inRect);
            var rect = inRect.ContractedBy(12f);

            var titleRect = new Rect(rect.x, rect.y, rect.width - 140f, 30f);
            GUI.color = CorvusStyle.TextPrimary;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "ProductionWindowTitleWithCount".Translate(filteredRecipes.Count));

            var tagRect = new Rect(rect.xMax - 116f, rect.y + 4f, 76f, 18f);
            float tagHover = GetHoverAmount("header_tag", Mouse.IsOver(tagRect));
            CorvusStyle.DrawHeaderTag(tagRect, "COG OPS", tagHover);
            TooltipHandler.TipRegion(tagRect, "Corvus Operations Group");

            var closeRect = new Rect(rect.xMax - 28f, rect.y + 1f, 24f, 24f);
            float closeHover = GetHoverAmount("window_close", Mouse.IsOver(closeRect));
            if (CorvusStyle.DrawIconButton(closeRect, "×", closeHover, true, false))
            {
                Close();
                return;
            }
            TooltipHandler.TipRegion(closeRect, "Close");

            var filterY = titleRect.yMax + 12f;
            var labelHeight = 18f;
            var filterHeight = 28f;
            var spacing = 10f;
            int activeFilterCount = GetActiveFilterCount();

            var totalFilterWidth = rect.width - 120f; // Reserve space for reset button
            var workstationWidth = totalFilterWidth * 0.20f;
            var categoryWidth = totalFilterWidth * 0.15f;
            var availabilityWidth = totalFilterWidth * 0.18f;
            var sourceWidth = totalFilterWidth * 0.20f;
            var searchWidth = totalFilterWidth * 0.22f;

            var filterPanelRect = new Rect(rect.x, filterY - 8f, rect.width, labelHeight + filterHeight + 44f);
            CorvusStyle.DrawPanel(filterPanelRect);

            var filterHeaderRect = new Rect(filterPanelRect.x + 12f, filterPanelRect.y + 6f, filterPanelRect.width - 24f, 20f);
            CorvusStyle.DrawSectionHeader(filterHeaderRect, "Filters");

            if (activeFilterCount > 0)
            {
                var activeChipRect = new Rect(filterPanelRect.xMax - 78f, filterPanelRect.y + 6f, 66f, 18f);
                CorvusStyle.DrawBadge(activeChipRect, $"{activeFilterCount} ACTIVE", CorvusStyle.Accent);
            }

            Text.Font = GameFont.Tiny;
            var workstationLabelRect = new Rect(rect.x + 12f, filterY + 20f, workstationWidth, labelHeight);
            GUI.color = CorvusStyle.TextSecondary;
            Widgets.Label(workstationLabelRect, "FilterByWorkstation".Translate());
            
            var categoryLabelRect = new Rect(workstationLabelRect.xMax + spacing, filterY, categoryWidth, labelHeight);
            categoryLabelRect.y = workstationLabelRect.y;
            Widgets.Label(categoryLabelRect, "FilterByCategory".Translate());
            
            var availabilityLabelRect = new Rect(categoryLabelRect.xMax + spacing, filterY, availabilityWidth, labelHeight);
            availabilityLabelRect.y = workstationLabelRect.y;
            Widgets.Label(availabilityLabelRect, "FilterByAvailability".Translate());
            
            var modLabelRect = new Rect(availabilityLabelRect.xMax + spacing, filterY, sourceWidth, labelHeight);
            modLabelRect.y = workstationLabelRect.y;
            Widgets.Label(modLabelRect, "FilterByMod".Translate());
            
            var searchLabelRect = new Rect(modLabelRect.xMax + spacing, filterY, searchWidth, labelHeight);
            searchLabelRect.y = workstationLabelRect.y;
            Widgets.Label(searchLabelRect, "FilterBySearchLabel".Translate());
            
            Text.Font = GameFont.Small;
            var controlsY = workstationLabelRect.y + labelHeight + 6f;

            var workstationRect = new Rect(rect.x + 12f, controlsY, workstationWidth, filterHeight);
            if (CorvusStyle.DrawButton(workstationRect, selectedWorkstation, GetHoverAmount("filter_workstation", Mouse.IsOver(workstationRect))))
            {
                var workstationOptions = availableWorkstations.Select(w => 
                    new FloatMenuOption(w, () => { selectedWorkstation = w; FilterRecipes(); })).ToList();
                Find.WindowStack.Add(new FloatMenu(workstationOptions));
            }
            
            var categoryRect = new Rect(workstationRect.xMax + spacing, controlsY, categoryWidth, filterHeight);
            if (CorvusStyle.DrawButton(categoryRect, selectedCategory, GetHoverAmount("filter_category", Mouse.IsOver(categoryRect))))
            {
                var floatMenu = new FloatMenu(categories.Select(c => 
                    new FloatMenuOption(c, () => { selectedCategory = c; FilterRecipes(); })).ToList());
                Find.WindowStack.Add(floatMenu);
            }

            var availabilityRect = new Rect(categoryRect.xMax + spacing, controlsY, availabilityWidth, filterHeight);
            if (CorvusStyle.DrawButton(availabilityRect, GetAvailabilityDisplayName(availabilityFilter), GetHoverAmount("filter_availability", Mouse.IsOver(availabilityRect)), true, availabilityFilter != AvailabilityFilter.Available))
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
            
            var modRect = new Rect(availabilityRect.xMax + spacing, controlsY, sourceWidth, filterHeight);
            if (CorvusStyle.DrawButton(modRect, selectedMod, GetHoverAmount("filter_mod", Mouse.IsOver(modRect))))
            {
                var modOptions = availableMods.Select(m => 
                    new FloatMenuOption(m, () => { selectedMod = m; FilterRecipes(); })).ToList();
                Find.WindowStack.Add(new FloatMenu(modOptions));
            }

            var searchRect = new Rect(modRect.xMax + spacing, controlsY, searchWidth, filterHeight);
            CorvusStyle.DrawPanel(searchRect, true);
            var newSearchText = Widgets.TextField(searchRect, searchText);
            if (newSearchText != searchText)
            {
                searchText = newSearchText;
                FilterRecipes();
            }
            
            var resetRect = new Rect(searchRect.xMax + spacing, controlsY, 90f, filterHeight);
            if (CorvusStyle.DrawButton(resetRect, "ResetFilters".Translate(), GetHoverAmount("filter_reset", Mouse.IsOver(resetRect))))
            {
                ResetAllFilters();
            }

            var summaryY = controlsY + filterHeight + 8f;
            var summaryRect = new Rect(filterPanelRect.x + 12f, summaryY, filterPanelRect.width - 24f, 18f);
            GUI.color = CorvusStyle.TextMuted;
            Text.Font = GameFont.Tiny;
            Widgets.Label(summaryRect, activeFilterCount > 0
                ? $"Active filters narrow the recipe list to {filteredRecipes.Count} entries."
                : "Default view shows recipes with available workbench and materials.");

            var remainingHeight = rect.height - (controlsY + filterHeight + 20f);
            var remainingY = filterPanelRect.yMax + 10f;
            remainingHeight = rect.height - (remainingY - rect.y);
            var paneGap = 8f;
            var recipeListRect = new Rect(rect.x, remainingY, rect.width * 0.58f - paneGap * 0.5f, remainingHeight);
            CorvusStyle.DrawPanel(recipeListRect);
            DrawRecipeList(recipeListRect);
            
            var billListRect = new Rect(recipeListRect.xMax + paneGap, remainingY, rect.xMax - (recipeListRect.xMax + paneGap), remainingHeight);
            CorvusStyle.DrawPanel(billListRect);
            DrawBillList(billListRect);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWordWrap;
                GUI.color = oldColor;
            }
        }

        private void DrawRecipeList(Rect rect)
        {
            Text.Font = GameFont.Small;
            
            var headerRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 25f);
            CorvusStyle.DrawSectionHeader(headerRect, "RecipesHeader".Translate());
            
            var listRect = new Rect(rect.x + 8f, rect.y + 36f, rect.width - 16f, rect.height - 44f);
            var itemHeight = 104f;
            var contentHeight = filteredRecipes.Count * itemHeight;
            var viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);
            
            CorvusStyle.BeginStyledScrollView(listRect, ref scrollPosition, viewRect);
            
            var curY = 0f;
            foreach (var recipeInfo in filteredRecipes)
            {
                var itemRect = new Rect(0f, curY, viewRect.width, itemHeight - 5f);
                DrawRecipeItem(itemRect, recipeInfo);
                curY += itemHeight;
            }
            
            CorvusStyle.EndStyledScrollView();
        }

        private void DrawRecipeItem(Rect rect, RecipeInfo recipeInfo)
        {
            float hover = GetHoverAmount("recipe_" + recipeInfo.recipe.defName, Mouse.IsOver(rect));
            CorvusStyle.DrawListRow(rect, hover);

            var recipe = recipeInfo.recipe;
            var innerRect = rect.ContractedBy(8f);
            
            string sourceText = recipeInfo.modSource;
            var addBillRect = new Rect(innerRect.xMax - 32f, innerRect.y + 20f, 28f, 28f);

            float titleX = innerRect.x;
            if (recipeInfo.primaryProductDef != null)
            {
                var productIconRect = new Rect(innerRect.x, innerRect.y + 1f, 18f, 18f);
                Widgets.DefIcon(productIconRect, recipeInfo.primaryProductDef);
                TooltipHandler.TipRegion(productIconRect, recipeInfo.primaryProductDef.LabelCap);
                titleX = productIconRect.xMax + 6f;
            }

            var nameRect = new Rect(titleX, innerRect.y, innerRect.width - 180f - (titleX - innerRect.x), 20f);
            GUI.color = CorvusStyle.TextPrimary;
            Widgets.Label(nameRect, recipe.label.CapitalizeFirst());
            
            var infoButtonRect = new Rect(nameRect.xMax + 5f, innerRect.y - 2f, 24f, 24f);
            var producedThing = recipe.ProducedThingDef;
            if (producedThing != null && Widgets.InfoCardButton(infoButtonRect, producedThing))
            {
            }
            
            var categoryRect = new Rect(innerRect.x, innerRect.y + 24f, 92f, 18f);
            CorvusStyle.DrawBadge(categoryRect, recipeInfo.category, CorvusStyle.AccentDim);

            var stateLabel = recipeInfo.hasWorkbench
                ? (recipeInfo.hasMaterials ? "READY" : "NO MAT")
                : "NO BENCH";
            var stateColor = recipeInfo.hasWorkbench
                ? (recipeInfo.hasMaterials ? CorvusStyle.Success : CorvusStyle.Warning)
                : CorvusStyle.Danger;
            var stateRect = new Rect(addBillRect.x - 78f, innerRect.y + 24f, 70f, 18f);
            CorvusStyle.DrawBadge(stateRect, stateLabel, stateColor);

            var sourceRect = new Rect(categoryRect.xMax + 8f, innerRect.y + 25f, stateRect.x - (categoryRect.xMax + 14f), 16f);
            GUI.color = CorvusStyle.TextMuted;
            Text.Font = GameFont.Tiny;
            Widgets.Label(sourceRect, sourceText);
            
            var dividerRect = new Rect(innerRect.x, innerRect.y + 47f, innerRect.width, 1f);
            CorvusStyle.DrawSeparator(dividerRect);

            var workbenchRect = new Rect(innerRect.x, innerRect.y + 54f, innerRect.width * 0.52f, 16f);
            var workbenchText = recipeInfo.workbenchDef?.label ?? "SourceUnknown".Translate().ToString();
            GUI.color = CorvusStyle.TextMuted;
            Widgets.Label(new Rect(workbenchRect.x, workbenchRect.y, 52f, 16f), "BENCH");
            GUI.color = recipeInfo.hasWorkbench ? CorvusStyle.Success : CorvusStyle.Warning;
            Widgets.Label(new Rect(workbenchRect.x + 50f, workbenchRect.y, workbenchRect.width - 50f, 16f), workbenchText);
            
            var ingredientsRect = new Rect(innerRect.x + innerRect.width * 0.52f, innerRect.y + 54f, innerRect.width * 0.48f, 16f);
            Text.Font = GameFont.Tiny;
            var ingredientsText = GetIngredientsText(recipe);
            GUI.color = CorvusStyle.TextMuted;
            Widgets.Label(new Rect(ingredientsRect.x, ingredientsRect.y, 58f, 16f), "INPUTS");
            float iconStripWidth = DrawIngredientIcons(recipe, ingredientsRect.x + 52f, ingredientsRect.y, 44f);
            GUI.color = CorvusStyle.TextSecondary;
            Widgets.Label(new Rect(ingredientsRect.x + 52f + iconStripWidth, ingredientsRect.y, ingredientsRect.width - 52f - iconStripWidth, 16f), ingredientsText);
            
            var skillsRect = new Rect(innerRect.x, innerRect.y + 72f, innerRect.width - 42f, 16f);
            var skillsText = GetSkillsText(recipe);
            GUI.color = CorvusStyle.TextMuted;
            Widgets.Label(new Rect(skillsRect.x, skillsRect.y, 52f, 16f), "WORK");
            GUI.color = CorvusStyle.TextSecondary;
            Widgets.Label(new Rect(skillsRect.x + 44f, skillsRect.y, skillsRect.width - 44f, 16f), skillsText);
            Text.Font = GameFont.Small;
            
            var canCreateBill = recipeInfo.CanCreateBill();
            float addHover = GetHoverAmount("recipe_add_" + recipeInfo.recipe.defName, Mouse.IsOver(addBillRect));
            if (CorvusStyle.DrawIconButton(addBillRect, "+", addHover, canCreateBill, canCreateBill && recipeInfo.hasMaterials) && canCreateBill)
            {
                recipeInfo.CreateBill(1, CustomRepeatMode.DoXTimes);
            }

            string addTooltip = canCreateBill
                ? "TooltipAddBill".Translate()
                : "MessageNoWorkbenchForBill".Translate();
            TooltipHandler.TipRegion(addBillRect, addTooltip);
            
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

        private ThingDef GetCyclingIngredientIconDef(IngredientCount ingredient, int ingredientIndex)
        {
            if (ingredient?.filter?.AllowedThingDefs == null)
            {
                return null;
            }

            var defs = ingredient.filter.AllowedThingDefs
                .Where(def => def != null)
                .OrderBy(def => def.defName)
                .ToList();

            if (defs.Count == 0)
            {
                return null;
            }

            if (defs.Count == 1)
            {
                return defs[0];
            }

            int tick = Mathf.FloorToInt(Time.realtimeSinceStartup * 0.75f);
            int index = Mathf.Abs(tick + ingredientIndex) % defs.Count;
            return defs[index];
        }

        private float DrawIngredientIcons(RecipeDef recipe, float startX, float y, float maxWidth)
        {
            if (recipe.ingredients?.Any() != true)
            {
                return 0f;
            }

            const float iconSize = 14f;
            const float spacing = 2f;
            float x = startX;
            int drawnIcons = 0;

            int ingredientIndex = 0;
            foreach (var ingredient in recipe.ingredients.Take(2))
            {
                ThingDef iconDef = GetCyclingIngredientIconDef(ingredient, ingredientIndex);
                if (iconDef == null) continue;

                if (x + iconSize > startX + maxWidth)
                {
                    break;
                }

                Rect iconRect = new Rect(x, y + 1f, iconSize, iconSize);
                Widgets.DefIcon(iconRect, iconDef);
                TooltipHandler.TipRegion(iconRect, iconDef.LabelCap);
                x += iconSize + spacing;
                drawnIcons++;
                ingredientIndex++;
            }

            int extraCount = (recipe.ingredients?.Count ?? 0) - drawnIcons;
            if (extraCount > 0 && x + 16f <= startX + maxWidth)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = CorvusStyle.TextMuted;
                Widgets.Label(new Rect(x, y, 16f, 16f), $"+{extraCount}");
                GUI.color = Color.white;
                x += 16f + spacing;
            }

            return Mathf.Max(0f, x - startX + 4f);
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
            
            if (skills.Any())
            {
                return string.Join(", ", skills);
            }

            return "No skill requirements".Translate().ToString();
        }

        private void DrawBillList(Rect rect)
        {
            Text.Font = GameFont.Small;
            
            var headerRect = new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 25f);
            CorvusStyle.DrawSectionHeader(headerRect, "BillsHeader".Translate());
            
            var relevantBills = GetRelevantBills();
            
            var listRect = new Rect(rect.x + 8f, rect.y + 36f, rect.width - 16f, rect.height - 44f);
            
            if (relevantBills.Count == 0)
            {
                var noBillsRect = new Rect(listRect.x + 10f, listRect.y + 10f, listRect.width - 20f, 30f);
                GUI.color = CorvusStyle.TextMuted;
                Widgets.Label(noBillsRect, "NoBillsFound".Translate());
                GUI.color = Color.white;
                return;
            }
            
            var itemHeight = 94f;
            var contentHeight = relevantBills.Count * itemHeight;
            var viewRect = new Rect(0f, 0f, listRect.width - 20f, contentHeight);
            
            CorvusStyle.BeginStyledScrollView(listRect, ref billScrollPosition, viewRect);
            
            var curY = 0f;
            foreach (var billInfo in relevantBills)
            {
                var itemRect = new Rect(0f, curY, viewRect.width, itemHeight - 5f);
                DrawBillItem(itemRect, billInfo);
                curY += itemHeight;
            }
            
            CorvusStyle.EndStyledScrollView();
        }

        private List<BillInfo> GetRelevantBills()
        {
            var relevantBills = new List<BillInfo>();
            
            if (Find.CurrentMap?.listerThings == null) return relevantBills;
            var filteredRecipeDefs = new HashSet<RecipeDef>(filteredRecipes.Select(r => r.recipe));
            
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
                        .Where(b => filteredRecipeDefs.Contains(b.recipe))
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
            float hover = GetHoverAmount("bill_" + billInfo.bill.GetHashCode(), Mouse.IsOver(rect));
            CorvusStyle.DrawListRow(rect, hover);

            var bill = billInfo.bill;
            var workbench = billInfo.workbench;
            var innerRect = rect.ContractedBy(8f);
            ThingDef productDef = bill.recipe?.ProducedThingDef ?? bill.recipe?.products?.Select(p => p?.thingDef).FirstOrDefault(t => t != null);
            
            float titleX = innerRect.x;
            if (productDef != null)
            {
                var productIconRect = new Rect(innerRect.x, innerRect.y + 1f, 18f, 18f);
                Widgets.DefIcon(productIconRect, productDef);
                TooltipHandler.TipRegion(productIconRect, productDef.LabelCap);
                titleX = productIconRect.xMax + 6f;
            }

            var nameRect = new Rect(titleX, innerRect.y, innerRect.width - 34f - (titleX - innerRect.x), 20f);
            GUI.color = CorvusStyle.TextPrimary;
            Widgets.Label(nameRect, bill.LabelCap);
            
            var workbenchRect = new Rect(innerRect.x, innerRect.y + 22f, innerRect.width - 98f, 16f);
            GUI.color = CorvusStyle.TextSecondary;
            Text.Font = GameFont.Tiny;
            Widgets.Label(workbenchRect, "@ " + workbench.LabelCap);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            if (bill.recipe != null)
            {
                var billInputIconsRect = new Rect(innerRect.xMax - 92f, innerRect.y + 22f, 70f, 16f);
                DrawIngredientIcons(bill.recipe, billInputIconsRect.x, billInputIconsRect.y, billInputIconsRect.width);
            }

            var deleteRect = new Rect(innerRect.xMax - 24f, innerRect.y + 2f, 20f, 20f);
            if (Mouse.IsOver(deleteRect))
            {
                GUI.color = new Color(CorvusStyle.Danger.r, CorvusStyle.Danger.g, CorvusStyle.Danger.b, 0.12f);
                GUI.DrawTexture(deleteRect.ExpandedBy(2f), BaseContent.WhiteTex);
                GUI.color = Color.white;
            }
            if (Widgets.ButtonImage(deleteRect, TexButton.Delete))
            {
                if (workbench is IBillGiver billGiver)
                {
                    billGiver.BillStack.Delete(bill);
                    Messages.Message($"Deleted bill: {bill.LabelCap}", MessageTypeDefOf.NeutralEvent);
                }
            }
            TooltipHandler.TipRegion(deleteRect, "TooltipDeleteBill".Translate());

            var dividerRect = new Rect(innerRect.x, innerRect.y + 45f, innerRect.width, 1f);
            CorvusStyle.DrawSeparator(dividerRect);
            
            var controlsY = innerRect.y + 54f;
            
            var minusRect = new Rect(innerRect.x + 4f, controlsY, 24f, 24f);
            if (CorvusStyle.DrawIconButton(minusRect, "-", GetHoverAmount("bill_minus_" + bill.GetHashCode(), Mouse.IsOver(minusRect))))
            {
                ModifyBillCount(bill, -1);
            }
            TooltipHandler.TipRegion(minusRect, "Decrease bill count");
            
            var countRect = new Rect(minusRect.xMax + 4f, controlsY, 40f, 24f);
            CorvusStyle.DrawPanel(countRect, true);
            string countText = GetBillCountText(bill);
            var newCountText = Widgets.TextField(countRect, countText);
            if (newCountText != countText && int.TryParse(newCountText, out int newCount) && newCount > 0)
            {
                SetBillCount(bill, newCount);
            }
            
            var plusRect = new Rect(countRect.xMax + 4f, controlsY, 24f, 24f);
            if (CorvusStyle.DrawIconButton(plusRect, "+", GetHoverAmount("bill_plus_" + bill.GetHashCode(), Mouse.IsOver(plusRect))))
            {
                ModifyBillCount(bill, 1);
            }
            TooltipHandler.TipRegion(plusRect, "Increase bill count");
            
            var modeRect = new Rect(plusRect.xMax + 8f, controlsY, 46f, 24f);
            var modeText = GetBillModeText(bill);
            float modeHover = GetHoverAmount("bill_mode_" + bill.GetHashCode(), Mouse.IsOver(modeRect));
            CorvusStyle.DrawChip(modeRect, modeText, modeHover, bill is Bill_Production prodBill && prodBill.repeatMode == BillRepeatModeDefOf.Forever);
            if (Widgets.ButtonInvisible(modeRect))
            {
                ShowBillModeMenu(bill);
            }
            TooltipHandler.TipRegion(modeRect, "Change repeat mode");
            
            var detailsRect = new Rect(modeRect.xMax + 8f, controlsY, 34f, 24f);
            if (CorvusStyle.DrawSmallButton(detailsRect, "CFG", GetHoverAmount("bill_details_" + bill.GetHashCode(), Mouse.IsOver(detailsRect))))
            {
                if (bill is Bill_Production productionBill)
                {
                    Find.WindowStack.Add(new Dialog_BillConfig(productionBill, workbench.Position));
                }
            }
            TooltipHandler.TipRegion(detailsRect, "TooltipBillDetails".Translate());
        }

        private string GetBillCountText(Bill bill)
        {
            if (!(bill is Bill_Production productionBill)) return "1";

            if (productionBill.repeatMode == BillRepeatModeDefOf.Forever) return "-";
            if (productionBill.repeatMode == BillRepeatModeDefOf.RepeatCount) return productionBill.repeatCount.ToString();
            if (productionBill.repeatMode == BillRepeatModeDefOf.TargetCount) return productionBill.targetCount.ToString();
            return "1";
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

        private float GetHoverAmount(string elementId, bool isHovered)
        {
            if (!hoverStates.TryGetValue(elementId, out float current))
            {
                current = 0f;
            }

            float target = isHovered ? 1f : 0f;
            current = Mathf.MoveTowards(current, target, Time.deltaTime * 8f);
            hoverStates[elementId] = current;
            return current;
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
