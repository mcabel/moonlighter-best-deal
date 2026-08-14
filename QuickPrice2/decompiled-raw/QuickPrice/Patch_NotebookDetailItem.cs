using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace QuickPrice
{
	[HarmonyPatch(typeof(NotebookPanel), "DetailItem")]
	public class Patch_NotebookDetailItem
	{
		private static Color tooCheapDefault;

		private static Color cheapDefault;

		private static Color expensiveDefault;

		private static Color tooExpensiveDefault;

		[HarmonyPostfix]
		private static void Postfix(NotebookPanel __instance, ItemMaster item, bool unlocked)
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0059: Unknown result type (might be due to invalid IL or missing references)
			//IL_005e: Unknown result type (might be due to invalid IL or missing references)
			//IL_007a: Unknown result type (might be due to invalid IL or missing references)
			//IL_007f: Unknown result type (might be due to invalid IL or missing references)
			//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
			//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
			//IL_0117: Unknown result type (might be due to invalid IL or missing references)
			//IL_011e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0145: Unknown result type (might be due to invalid IL or missing references)
			//IL_014c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0173: Unknown result type (might be due to invalid IL or missing references)
			//IL_017a: Unknown result type (might be due to invalid IL or missing references)
			if (tooCheapDefault.a == 0f)
			{
				tooCheapDefault = ((Graphic)__instance.textLastTooCheap).color;
			}
			if (cheapDefault.a == 0f)
			{
				cheapDefault = ((Graphic)__instance.textLastCheap).color;
			}
			if (expensiveDefault.a == 0f)
			{
				expensiveDefault = ((Graphic)__instance.textLastExpensive).color;
			}
			if (tooExpensiveDefault.a == 0f)
			{
				tooExpensiveDefault = ((Graphic)__instance.textLastTooExpensive).color;
			}
			if (!Plugin.EnableNotebookHighlight.Value)
			{
				RestoreDefaults(__instance);
			}
			else if (item != null)
			{
				ItemPriceManager instance = ItemPriceManager.Instance;
				ItemPriceInfo.Popularity popularity = instance.GetPopularity(item);
				instance.SetPopularity(item, ItemPriceInfo.Popularity.Neutral);
				Color val = default(Color);
				((Color)(ref val))._002Ector(0f, 0.8f, 0.2f);
				int lastPrice = instance.GetLastPrice(item, ItemPriceValoration.TooCheap);
				int num = instance.GetMinCorrectPrice(item) - 1;
				((Graphic)__instance.textLastTooCheap).color = ((lastPrice > 0 && lastPrice == num) ? val : tooCheapDefault);
				lastPrice = instance.GetLastPrice(item, ItemPriceValoration.Cheap);
				num = instance.GetMaxCorrectPrice(item);
				((Graphic)__instance.textLastCheap).color = ((lastPrice > 0 && lastPrice == num) ? val : cheapDefault);
				lastPrice = instance.GetLastPrice(item, ItemPriceValoration.Expensive);
				num = instance.GetMaxOverpricedPrice(item);
				((Graphic)__instance.textLastExpensive).color = ((lastPrice > 0 && lastPrice == num) ? val : expensiveDefault);
				lastPrice = instance.GetLastPrice(item, ItemPriceValoration.TooExpensive);
				num = instance.GetTooExpensiveLimitPrice(item);
				((Graphic)__instance.textLastTooExpensive).color = ((lastPrice > 0 && lastPrice == num) ? val : tooExpensiveDefault);
				instance.SetPopularity(item, popularity);
				if (Plugin.Debug.Value)
				{
					Plugin.LOG.LogInfo("Notebook highlight: " + item.nameKey);
				}
			}
		}

		private static void RestoreDefaults(NotebookPanel instance)
		{
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			//IL_0016: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			((Graphic)instance.textLastTooCheap).color = tooCheapDefault;
			((Graphic)instance.textLastCheap).color = cheapDefault;
			((Graphic)instance.textLastExpensive).color = expensiveDefault;
			((Graphic)instance.textLastTooExpensive).color = tooExpensiveDefault;
		}

		static Patch_NotebookDetailItem()
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000a: Unknown result type (might be due to invalid IL or missing references)
			//IL_000f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0014: Unknown result type (might be due to invalid IL or missing references)
			//IL_0019: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0023: Unknown result type (might be due to invalid IL or missing references)
			tooCheapDefault = Color.clear;
			cheapDefault = Color.clear;
			expensiveDefault = Color.clear;
			tooExpensiveDefault = Color.clear;
		}
	}
}
