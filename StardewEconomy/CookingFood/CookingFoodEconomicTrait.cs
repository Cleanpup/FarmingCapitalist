using System;

namespace FarmingCapitalist
{
    /// <summary>
    /// Mod-defined cooking-food traits used by cooking-food economy systems.
    /// Items may intentionally belong to more than one trait bucket.
    /// </summary>
    [Flags]
    internal enum CookingFoodEconomicTrait
    {
        None = 0,

        Meal = 1 << 0,
        Dessert = 1 << 1,
        Drink = 1 << 2,
        BuffFood = 1 << 3,
        RecipeOutput = 1 << 4,
        CookingIngredient = 1 << 5
    }
}
