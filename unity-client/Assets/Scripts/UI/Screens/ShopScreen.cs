using System.Collections;
using System.Collections.Generic;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// Booster and life purchases. Prices come from remote config (so an experiment can change
    /// them per player); every purchase carries an Idempotency-Key so a retried request cannot
    /// charge twice.
    /// </summary>
    public sealed class ShopScreen : UiScreen
    {
        private Text _coinsLabel;
        private Text _livesLabel;
        private Button _refillButton;
        private readonly Dictionary<string, Text> _ownedLabels = new Dictionary<string, Text>();
        private readonly Dictionary<string, Button> _buyButtons = new Dictionary<string, Button>();

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 20f, 40);
            CreateHeader(column, "Shop", () => App.Flow.GoHome());
            _coinsLabel = UiFactory.CreateLabel(column, "", UiTheme.HeadingSize, UiTheme.Warning, TextAnchor.MiddleLeft, FontStyle.Bold);

            foreach (string type in BoosterTypes.All)
            {
                BuildBoosterRow(column, type);
            }

            Image livesPanel = UiFactory.CreatePanel(column, "Lives", UiTheme.Panel);
            UiFactory.SetLayout(livesPanel.gameObject, preferredHeight: 150f, minHeight: 150f);
            UiFactory.AddHorizontalLayout(livesPanel.rectTransform, 16f, 20);
            _livesLabel = UiFactory.CreateLabel(livesPanel.transform, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(_livesLabel.gameObject, flexibleWidth: 1f);
            _refillButton = UiFactory.CreateButton(livesPanel.transform, "Refill", OnRefill, UiTheme.Accent, UiTheme.SmallSize, 100f, 260f);

            UiFactory.CreateLabel(column, "Prices are served by remote config and may differ per experiment variant.",
                UiTheme.SmallSize, UiTheme.Muted);
            UiFactory.CreateSpacer(column);
        }

        private void BuildBoosterRow(RectTransform column, string type)
        {
            Image panel = UiFactory.CreatePanel(column, "Booster " + type, UiTheme.Panel);
            UiFactory.SetLayout(panel.gameObject, preferredHeight: 150f, minHeight: 150f);
            UiFactory.AddHorizontalLayout(panel.rectTransform, 16f, 20);

            RectTransform texts = UiFactory.CreateColumn(panel.transform, "Texts", 4f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(texts.gameObject, flexibleWidth: 1f);
            UiFactory.CreateLabel(texts, BoosterTypes.Label(type), UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            _ownedLabels[type] = UiFactory.CreateLabel(texts, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);

            string captured = type;
            _buyButtons[type] = UiFactory.CreateButton(panel.transform, "Buy", () => Run(Buy(captured)), UiTheme.Accent, UiTheme.SmallSize, 100f, 260f);
        }

        protected override void OnShown()
        {
            Refresh();
            if (App.State.Config == null)
            {
                Run(LoadConfig());
            }
        }

        private IEnumerator LoadConfig()
        {
            var config = new ApiResult<ClientConfigResponse>();
            yield return App.Api.GetJson(ApiRoutes.Config, config);
            if (!IsAlive) yield break;
            if (config.Ok)
            {
                App.State.SetConfig(config.Value);
                Refresh();
            }
            else
            {
                App.Flow.ShowError(config.Error);
            }
        }

        private Dictionary<string, int> Prices()
        {
            ClientConfigResponse config = App.State.Config;
            return config != null
                ? config.GetIntMap(ConfigKeys.BoosterPrices, ConfigKeys.DefaultBoosterPrices)
                : ConfigKeys.DefaultBoosterPrices;
        }

        private int RefillPrice()
        {
            ClientConfigResponse config = App.State.Config;
            return config != null ? config.GetInt(ConfigKeys.LifeRefillPrice, ConfigKeys.DefaultLifeRefillPrice) : ConfigKeys.DefaultLifeRefillPrice;
        }

        private void Refresh()
        {
            GameState state = App.State;
            WalletSnapshot wallet = state.Wallet;
            Dictionary<string, int> prices = Prices();
            _coinsLabel.text = "Coins: " + (wallet != null ? TimeFormat.Number(wallet.coins) : "-");
            foreach (string type in BoosterTypes.All)
            {
                int price = prices.TryGetValue(type, out int p) ? p : 0;
                _ownedLabels[type].text = price + " coins  ·  owned " + state.BoosterCount(type);
                UiFactory.SetButtonLabel(_buyButtons[type], "Buy (" + price + ")");
                _buyButtons[type].interactable = wallet == null || wallet.coins >= price;
            }
            if (wallet != null)
            {
                bool full = wallet.lives >= wallet.maxLives;
                _livesLabel.text = "Lives " + wallet.lives + "/" + wallet.maxLives + (full ? "  ·  full" : "  ·  next in " + TimeFormat.Countdown(state.NextLifeInSecondsNow));
                _refillButton.interactable = !full && wallet.coins >= RefillPrice();
            }
            else
            {
                _livesLabel.text = "Lives -";
            }
            UiFactory.SetButtonLabel(_refillButton, "Refill (" + RefillPrice() + ")");
        }

        private IEnumerator Buy(string type)
        {
            var result = new ApiResult<PurchaseResult>();
            yield return App.Flow.BuyBooster(type, 1, result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Toast.Show("Bought " + result.Value.quantity + " x " + BoosterTypes.Label(type) + " for " + result.Value.coinsSpent + " coins");
                Refresh();
            }
            else
            {
                App.Flow.ShowError(result.Error);
            }
        }

        private void OnRefill()
        {
            WalletSnapshot wallet = App.State.Wallet;
            if (wallet != null && wallet.lives >= wallet.maxLives)
            {
                App.Toast.Show("Your lives are already full");
                return;
            }
            Run(Refill());
        }

        private IEnumerator Refill()
        {
            var result = new ApiResult<PurchaseResult>();
            yield return App.Flow.BuyLives(result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Toast.Show("Lives refilled for " + result.Value.coinsSpent + " coins");
                Refresh();
            }
            else
            {
                App.Flow.ShowError(result.Error);
            }
        }
    }
}
