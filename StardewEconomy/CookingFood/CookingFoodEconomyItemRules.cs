using StardewValley;
using SObject = StardewValley.Object;

namespace FarmingCapitalist
{
    /// <summary>
    /// Cooking-and-food-owned eligibility and normalization helpers.
    /// Membership intentionally stays curated to vanilla cooking recipe outputs,
    /// the requested pantry ingredients, and a small non-alcoholic beverage set.
    /// </summary>
    internal static class CookingFoodEconomyItemRules
    {
        private static readonly HashSet<string> RecipeOutputItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "194", // Fried Egg
            "195", // Omelet
            "196", // Salad
            "197", // Cheese Cauliflower
            "198", // Baked Fish
            "199", // Parsnip Soup
            "200", // Vegetable Medley
            "201", // Complete Breakfast
            "202", // Fried Calamari
            "203", // Strange Bun
            "204", // Lucky Lunch
            "205", // Fried Mushroom
            "206", // Pizza
            "207", // Bean Hotpot
            "208", // Glazed Yams
            "209", // Carp Surprise
            "210", // Hashbrowns
            "211", // Pancakes
            "212", // Salmon Dinner
            "213", // Fish Taco
            "214", // Crispy Bass
            "215", // Pepper Poppers
            "216", // Bread
            "218", // Tom Kha Soup
            "219", // Trout Soup
            "220", // Chocolate Cake
            "221", // Pink Cake
            "222", // Rhubarb Pie
            "223", // Cookie
            "224", // Spaghetti
            "225", // Fried Eel
            "226", // Spicy Eel
            "227", // Sashimi
            "228", // Maki Roll
            "229", // Tortilla
            "230", // Red Plate
            "231", // Eggplant Parmesan
            "232", // Rice Pudding
            "233", // Ice Cream
            "234", // Blueberry Tart
            "235", // Autumn's Bounty
            "236", // Pumpkin Soup
            "237", // Super Meal
            "238", // Cranberry Sauce
            "239", // Stuffing
            "240", // Farmer's Lunch
            "241", // Survival Burger
            "242", // Dish O' The Sea
            "243", // Miner's Treat
            "244", // Roots Platter
            "253", // Triple Shot Espresso
            "265", // Seafoam Pudding
            "456", // Algae Soup
            "457", // Pale Broth
            "604", // Plum Pudding
            "605", // Artichoke Dip
            "606", // Stir Fry
            "607", // Roasted Hazelnuts
            "608", // Pumpkin Pie
            "609", // Radish Salad
            "610", // Fruit Salad
            "611", // Blackberry Cobbler
            "612", // Cranberry Candy
            "618", // Bruschetta
            "648", // Coleslaw
            "649", // Fiddlehead Risotto
            "651", // Poppyseed Muffin
            "727", // Chowder
            "728", // Fish Stew
            "729", // Escargot
            "730", // Lobster Bisque
            "731", // Maple Bar
            "732", // Crab Cakes
            "733", // Shrimp Cocktail
            "903", // Ginger Ale
            "904", // Banana Pudding
            "905", // Mango Sticky Rice
            "906", // Poi
            "907", // Tropical Curry
            "921", // Squid Ink Ravioli
            "MossSoup" // Moss Soup
        };

        private static readonly HashSet<string> DessertItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "220", // Chocolate Cake
            "221", // Pink Cake
            "222", // Rhubarb Pie
            "223", // Cookie
            "232", // Rice Pudding
            "233", // Ice Cream
            "234", // Blueberry Tart
            "243", // Miner's Treat
            "604", // Plum Pudding
            "608", // Pumpkin Pie
            "611", // Blackberry Cobbler
            "612", // Cranberry Candy
            "651", // Poppyseed Muffin
            "731", // Maple Bar
            "904", // Banana Pudding
            "905"  // Mango Sticky Rice
        };

        private static readonly HashSet<string> DrinkItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "253", // Triple Shot Espresso
            "395", // Coffee
            "614", // Green Tea
            "903"  // Ginger Ale
        };

        private static readonly HashSet<string> BuffFoodItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "201", // Complete Breakfast
            "204", // Lucky Lunch
            "205", // Fried Mushroom
            "207", // Bean Hotpot
            "210", // Hashbrowns
            "211", // Pancakes
            "213", // Fish Taco
            "214", // Crispy Bass
            "215", // Pepper Poppers
            "218", // Tom Kha Soup
            "219", // Trout Soup
            "225", // Fried Eel
            "226", // Spicy Eel
            "230", // Red Plate
            "231", // Eggplant Parmesan
            "235", // Autumn's Bounty
            "236", // Pumpkin Soup
            "237", // Super Meal
            "238", // Cranberry Sauce
            "239", // Stuffing
            "240", // Farmer's Lunch
            "241", // Survival Burger
            "242", // Dish O' The Sea
            "243", // Miner's Treat
            "253", // Triple Shot Espresso
            "265", // Seafoam Pudding
            "395", // Coffee
            "614", // Green Tea
            "727", // Chowder
            "728", // Fish Stew
            "729", // Escargot
            "730", // Lobster Bisque
            "731", // Maple Bar
            "732", // Crab Cakes
            "733", // Shrimp Cocktail
            "903", // Ginger Ale
            "904", // Banana Pudding
            "905", // Mango Sticky Rice
            "907", // Tropical Curry
            "921"  // Squid Ink Ravioli
        };

        private static readonly HashSet<string> CookingIngredientItemIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "245", // Sugar
            "246", // Wheat Flour
            "247", // Oil
            "419", // Vinegar
            "423"  // Rice
        };

        public static bool IsCookingFoodEligible(Item? item)
        {
            return TryGetCookingFoodObject(item, out _);
        }

        public static bool TryGetCookingFoodObject(Item? item, out SObject cookingFoodObject)
        {
            cookingFoodObject = null!;
            if (item is not SObject obj || string.IsNullOrWhiteSpace(obj.ItemId))
                return false;

            if (!TryNormalizeCookingFoodItemId(obj.ItemId, out string normalizedItemId))
                return false;

            if (!IsCookingFoodItemId(normalizedItemId))
                return false;

            cookingFoodObject = obj;
            return true;
        }

        public static bool TryNormalizeCookingFoodItemId(string? rawCookingFoodItemId, out string normalizedCookingFoodItemId)
        {
            normalizedCookingFoodItemId = string.Empty;
            if (string.IsNullOrWhiteSpace(rawCookingFoodItemId))
                return false;

            string candidate = rawCookingFoodItemId.Trim();
            if (candidate.StartsWith("(O)", StringComparison.OrdinalIgnoreCase))
                candidate = candidate.Substring(3);

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            normalizedCookingFoodItemId = candidate;
            return true;
        }

        public static bool TryCreateCookingFoodObject(string rawItemId, out SObject? cookingFoodObject)
        {
            cookingFoodObject = null;
            if (!TryNormalizeCookingFoodItemId(rawItemId, out string normalizedCookingFoodItemId))
                return false;

            cookingFoodObject = ItemRegistry.Create<SObject>("(O)" + normalizedCookingFoodItemId, allowNull: true);
            return cookingFoodObject is not null
                && IsCookingFoodItemId(normalizedCookingFoodItemId);
        }

        public static bool IsMeal(Item? item)
        {
            return TryGetNormalizedCookingFoodItemId(item, out string itemId)
                && IsRecipeOutputItemId(itemId)
                && !DessertItemIds.Contains(itemId)
                && !DrinkItemIds.Contains(itemId);
        }

        public static bool IsDessert(Item? item)
        {
            return TryGetNormalizedCookingFoodItemId(item, out string itemId)
                && DessertItemIds.Contains(itemId);
        }

        public static bool IsDrink(Item? item)
        {
            return TryGetNormalizedCookingFoodItemId(item, out string itemId)
                && DrinkItemIds.Contains(itemId);
        }

        public static bool IsBuffFood(Item? item)
        {
            return TryGetNormalizedCookingFoodItemId(item, out string itemId)
                && BuffFoodItemIds.Contains(itemId);
        }

        public static bool IsRecipeOutput(Item? item)
        {
            return TryGetNormalizedCookingFoodItemId(item, out string itemId)
                && IsRecipeOutputItemId(itemId);
        }

        public static bool IsCookingIngredient(Item? item)
        {
            return TryGetNormalizedCookingFoodItemId(item, out string itemId)
                && CookingIngredientItemIds.Contains(itemId);
        }

        public static bool IsCookingFoodItemId(string? cookingFoodItemId)
        {
            return TryNormalizeCookingFoodItemId(cookingFoodItemId, out string normalizedCookingFoodItemId)
                && (RecipeOutputItemIds.Contains(normalizedCookingFoodItemId)
                    || DrinkItemIds.Contains(normalizedCookingFoodItemId)
                    || CookingIngredientItemIds.Contains(normalizedCookingFoodItemId));
        }

        private static bool TryGetNormalizedCookingFoodItemId(Item? item, out string cookingFoodItemId)
        {
            cookingFoodItemId = string.Empty;
            return item is SObject obj
                && TryNormalizeCookingFoodItemId(obj.ItemId, out cookingFoodItemId)
                && IsCookingFoodItemId(cookingFoodItemId);
        }

        private static bool IsRecipeOutputItemId(string cookingFoodItemId)
        {
            return RecipeOutputItemIds.Contains(cookingFoodItemId);
        }
    }
}
