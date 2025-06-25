# [COG] Corvus Production UI

A comprehensive production management mod for RimWorld that provides an advanced interface for planning, filtering, and managing your colony's production needs.

# COLONIAL PRODUCTION SOLUTIONS

## ProducTech Management Interface v3.4™
**Maximizing Colony Output Through Superior Information Architecture**
At Colonial Production Solutions, we believe that every colony deserves peak efficiency. Our ProducTech Management Interface represents decades of research into optimal production workflows, field-tested across thousands of rim settlements where failure isn't just inconvenient—it's fatal.
**evolutionary Features:**
- **Streamlined Production Oversight:** Monitor all manufacturing processes from a single, intuitive dashboard. Because micromanagement is for colonies that can afford to waste time.
- **Enhanced Resource Allocation:** Our proprietary algorithms ensure maximum material utilization, reducing waste by up to 87%*
- **Automated Priority Scheduling:** Let our system decide what your colonists should be making. After all, we know what's best for productivity.
- **Real-Time Efficiency Metrics:** Track worker performance with precision that would make a corporate overseer weep tears of joy.

*"Since installing the Colonial Production Solutions interface, our textile output increased 400%. The workers barely have time to complain anymore!"*
— Site Manager K████, Mining Colony Designation 7-Alpha

**Colonial Production Solutions: Engineering Tomorrow's Workforce, Today**

*Results based on internal testing at Corvus Operations Group facilities. Colonial Production Solutions is not liable for workplace accidents, worker exhaustion, or mysterious disappearances of "underperforming" personnel. Side effects may include: obsessive production scheduling, inability to sleep without checking quotas, and an overwhelming urge to expand manufacturing capacity.[/i]

---

*A Corvus Operations Group Subsidiary | Your Success Is Our Profit™*

## Features

### 🔍 Advanced Recipe Browser
- **Comprehensive Recipe Database**: Browse all available recipes with detailed information
- **Smart Filtering System**: 
  - **Workstation Filter**: Filter by specific workbenches (existing ones marked with ✓)
  - **Type Filter**: Categories include Ranged Weapons, Melee Weapons, Apparel, Food, Medicine, Materials, Buildings, Drugs, and Other
  - **Availability Filter**: Show Available, No Materials, or No Workbench recipes
  - **Source Filter**: Filter by Vanilla, DLC content (Royalty, Ideology, Biotech, Anomaly), or specific mods
  - **Search Filter**: Text-based search for recipe names
- **Reset Functionality**: Quickly clear all filters to start fresh

### 📋 Detailed Recipe Information
- **Material Requirements**: See exactly what materials and quantities are needed
- **Workbench Information**: Know which workstation is required and if you have it
- **Skill Requirements**: View required skills, learning factors, and work amounts
- **Info Cards**: Click the info button to view detailed recipe information
- **Color-Coded Availability**: Green for available, red for missing requirements

### ⚙️ Intelligent Bill Management
- **Split Interface**: 60/40 layout with recipe browser on left, bill management on right
- **Smart Bill Creation**: Automatically selects the workbench with the fewest existing bills
- **Multiple Repeat Modes**: 
  - "Do X times" (x5)
  - "Do until you have X" (≤5) 
  - "Do forever" (∞)
- **Real-Time Bill Filtering**: See only bills relevant to your current recipe filters
- **Chronological Bill Organization**: Bills are ordered by position across all relevant workbenches
- **Full Bill Controls**: Modify counts, change modes, access detailed configuration, or delete bills

### 🎯 Quality of Life Features
- **Best Workbench Selection**: Automatically chooses optimal workstation for new bills
- **Native Integration**: Uses RimWorld's built-in bill configuration dialogs
- **Responsive Design**: Clean, modern interface that fits RimWorld's aesthetic
- **No Performance Impact**: Efficient filtering and caching for smooth operation

## Installation
1. Download the latest release from the [Releases](../../releases) page
2. Extract the contents into your RimWorld/Mods folder
3. Enable the mod in the mod configuration menu in-game
4. The "Production" button will appear in your bottom toolbar

## Compatibility
- **RimWorld Versions**: 1.5, 1.6
- **Save Game Safe**: Can be added to existing saves
- **Mod Compatibility**: Works with all mods that add recipes through standard RimWorld systems
- **Performance**: Optimized for colonies with large numbers of recipes and bills

## Usage
1. Click the "Production" button in your bottom toolbar
2. Use the filters to find the recipes you need
3. Click "Add Bill" to create bills on the best available workbench
4. Manage existing bills in the right panel with full controls
5. Use the "Details" button for advanced bill configuration
6. Use "Reset" to clear all filters and start over

## Development
This mod is built using C# and requires RimWorld assembly references to compile.

### Build Requirements
- .NET Framework 4.7.2
- RimWorld assembly references:
  - Assembly-CSharp.dll
  - UnityEngine.CoreModule.dll
  - UnityEngine.IMGUIModule.dll
  - UnityEngine.TextRenderingModule.dll

### Project Structure
- `Source/CorvusProductionUI/` - Main C# source code
- `About/` - Mod metadata and description
- `Defs/MainButtonDefs/` - UI button definition
- `Assemblies/` - Compiled mod assembly

## License
This mod is released under the MIT License. Feel free to modify and distribute according to the license terms.