using HarmonyLib;
using UnityEngine;

namespace QuickPrice
{
	[HarmonyPatch(typeof(NotebookPanel), "DetailItem")]
	public class Patch_NotebookDetailItem
	{
		// Cache the notebook labels' original text colors the first time we see them, so
		// RestoreDefaults() can put things back exactly as the base game left them.
		private static Color tooCheapDefault = Color.clear;

		private static Color cheapDefault = Color.clear;

		private static Color expensiveDefault = Color.clear;

		private static Color tooExpensiveDefault = Color.clear;

		[HarmonyPostfix]
		private static void Postfix(NotebookPanel __instance, ItemMaster item, bool unlocked)
		{
			if (tooCheapDefault.a == 0f)
			{
				tooCheapDefault = __instance.textLastTooCheap.color;
			}
			if (cheapDefault.a == 0f)
			{
				cheapDefault = __instance.textLastCheap.color;
			}
			if (expensiveDefault.a == 0f)
			{
				expensiveDefault = __instance.textLastExpensive.color;
			}
			if (tooExpensiveDefault.a == 0f)
			{
				tooExpensiveDefault = __instance.textLastTooExpensive.color;
			}

			if (!Plugin.EnableNotebookHighlight.Value)
			{
				RestoreDefaults(__instance);
				return;
			}

			if (item == null)
			{
				return;
			}

			ItemPriceManager instance = ItemPriceManager.Instance;

			// Reading GetLastPrice() below is influenced by the item's cached "popularity" state
			// (see ItemPriceManager.GetPopularity/GetCorrectPriceWithPopularity). Force it to
			// Neutral while we compute prices for the highlight comparison, then restore it, so
			// the notebook's on-screen highlight always reflects the underlying (non-popularity-biased)
			// price tiers rather than whatever popularity state happened to be cached at the time.
			ItemPriceInfo.Popularity popularity = instance.GetPopularity(item);
			instance.SetPopularity(item, ItemPriceInfo.Popularity.Neutral);

			Color highlight = new Color(0f, 0.8f, 0.2f);

			int lastPrice = instance.GetLastPrice(item, ItemPriceValoration.TooCheap);
			int tooCheapPrice = instance.GetMinCorrectPrice(item) - 1;
			__instance.textLastTooCheap.color = (lastPrice > 0 && lastPrice == tooCheapPrice) ? highlight : tooCheapDefault;

			lastPrice = instance.GetLastPrice(item, ItemPriceValoration.Cheap);
			int cheapPrice = instance.GetMaxCorrectPrice(item);
			__instance.textLastCheap.color = (lastPrice > 0 && lastPrice == cheapPrice) ? highlight : cheapDefault;

			lastPrice = instance.GetLastPrice(item, ItemPriceValoration.Expensive);
			int expensivePrice = instance.GetMaxOverpricedPrice(item);
			__instance.textLastExpensive.color = (lastPrice > 0 && lastPrice == expensivePrice) ? highlight : expensiveDefault;

			lastPrice = instance.GetLastPrice(item, ItemPriceValoration.TooExpensive);
			int tooExpensivePrice = instance.GetTooExpensiveLimitPrice(item);
			__instance.textLastTooExpensive.color = (lastPrice > 0 && lastPrice == tooExpensivePrice) ? highlight : tooExpensiveDefault;

			instance.SetPopularity(item, popularity);

			if (Plugin.Debug.Value)
			{
				Plugin.LOG.LogInfo("Notebook highlight: " + item.nameKey);
			}
		}

		private static void RestoreDefaults(NotebookPanel instance)
		{
			instance.textLastTooCheap.color = tooCheapDefault;
			instance.textLastCheap.color = cheapDefault;
			instance.textLastExpensive.color = expensiveDefault;
			instance.textLastTooExpensive.color = tooExpensiveDefault;
		}
	}
}
