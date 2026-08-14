using HarmonyLib;
using UnityEngine;

namespace QuickPrice
{
	[HarmonyPatch(typeof(ShowcaseSlotGUI), "SetItemStack")]
	public class Patch_AutoPrice
	{
		[HarmonyPostfix]
		private static void Postfix(ShowcaseSlotGUI __instance, ItemStack stack, bool forInitialize)
		{
			if (forInitialize || !Plugin.EnableAutoPrice.Value || (Object)(object)stack == (Object)null)
			{
				return;
			}
			ItemPriceManager instance = ItemPriceManager.Instance;
			if ((Object)(object)instance == (Object)null || (Object)(object)__instance.ownerShowcase == (Object)null || __instance.ownerShowcase.itemPositions == null || __instance.slotIndex < 0 || __instance.slotIndex >= __instance.ownerShowcase.itemPositions.Count)
			{
				return;
			}
			bool isCool = __instance.ownerShowcase.itemPositions[__instance.slotIndex].isCool;
			int num = CalculatePrice(stack.master, instance, isCool);
			if (num > 0)
			{
				stack.unitSellingPrice = num;
				AccessTools.Method(typeof(ShowcaseSlotGUI), "SetLabelNumber")?.Invoke(__instance, new object[2] { num, stack.Quantity });
				__instance.ownerShowcase.itemPositions[__instance.slotIndex].SetPrice(num * stack.Quantity);
				instance.SetLastSettedPrice(stack.master, num);
				if (Plugin.Debug.Value)
				{
					Plugin.LOG.LogInfo($"Auto-priced '{stack.master.nameKey}' to {num} (tier: {Plugin.PriceTier.Value}, isCool: {isCool})");
				}
			}
		}

		private static int CalculatePrice(ItemMaster item, ItemPriceManager mgr, bool isCool)
		{
			switch (Plugin.PriceTier.Value)
			{
			case "MaxPriceInTooCheap":
				return mgr.GetMinCorrectPrice(item, isCool) - 1;
			case "MaxCorrectPrice":
				return mgr.GetMaxCorrectPrice(item, hasHighBudget: false, isCool);
			case "MaxPriceInExpensive":
				return mgr.GetMaxOverpricedPrice(item, hasHighBudget: false, isCool);
			case "MaxPriceInTooExpensive":
				return mgr.GetTooExpensiveLimitPrice(item, hasHighBudget: false, isCool);
			case "MarketPrice":
				return mgr.GetCorrectPriceWithPopularity(item, isCool);
			default:
				return mgr.GetMaxCorrectPrice(item, hasHighBudget: false, isCool);
			}
		}
	}
}
