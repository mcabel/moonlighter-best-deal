using HarmonyLib;

namespace QuickPrice
{
	[HarmonyPatch(typeof(ShowcaseSlotGUI), "SetItemStack")]
	public class Patch_AutoPrice
	{
		[HarmonyPostfix]
		private static void Postfix(ShowcaseSlotGUI __instance, ItemStack stack, bool forInitialize)
		{
			if (forInitialize || !Plugin.EnableAutoPrice.Value || stack == null)
			{
				return;
			}

			ItemPriceManager instance = ItemPriceManager.Instance;
			if (instance == null
				|| __instance.ownerShowcase == null
				|| __instance.ownerShowcase.itemPositions == null
				|| __instance.slotIndex < 0
				|| __instance.slotIndex >= __instance.ownerShowcase.itemPositions.Count)
			{
				return;
			}

			bool isCool = __instance.ownerShowcase.itemPositions[__instance.slotIndex].isCool;
			int price = CalculatePrice(stack.master, instance, isCool);

			if (price > 0)
			{
				stack.unitSellingPrice = price;
				AccessTools.Method(typeof(ShowcaseSlotGUI), "SetLabelNumber")?.Invoke(__instance, new object[] { price, stack.Quantity });
				__instance.ownerShowcase.itemPositions[__instance.slotIndex].SetPrice(price * stack.Quantity);
				instance.SetLastSettedPrice(stack.master, price);
				if (Plugin.Debug.Value)
				{
					Plugin.LOG.LogInfo($"Auto-priced '{stack.master.nameKey}' to {price} (tier: {Plugin.PriceTier.Value}, isCool: {isCool})");
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
