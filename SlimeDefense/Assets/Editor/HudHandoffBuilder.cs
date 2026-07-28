#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds Main's HUD from the dark 2a design handoff. It is intentionally an
/// editor tool: the resulting hierarchy is ordinary serialized Unity UI and is
/// fully visible/editable without entering Play mode.
/// </summary>
[InitializeOnLoad]
public static class HudHandoffBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string RequestPath = "Assets/Editor/HudHandoffBuild.request";
    const string RoundedSpritePath = "Assets/UI/Generated/RoundedRect.png";
    const string NunitoFontPath = "Assets/UI/Fonts/Nunito-VariableFont_wght.ttf";
    const string MonoFontPath = "Assets/UI/Fonts/JetBrainsMono-VariableFont_wght.ttf";
    const string NunitoAssetPath = "Assets/UI/Fonts/Nunito SDF.asset";
    const string MonoAssetPath = "Assets/UI/Fonts/JetBrains Mono SDF.asset";

    static readonly Color32 Surface = new Color32(40, 43, 35, 240);
    static readonly Color32 Panel = new Color32(40, 43, 35, 247);
    static readonly Color32 Raised = new Color32(49, 53, 43, 255);
    static readonly Color32 RaisedHover = new Color32(59, 64, 52, 255);
    static readonly Color32 Primary = new Color32(236, 238, 228, 255);
    static readonly Color32 Secondary = new Color32(142, 145, 135, 255);
    static readonly Color32 Gold = new Color32(232, 199, 106, 255);
    static readonly Color32 GoldHover = new Color32(242, 214, 133, 255);
    static readonly Color32 Danger = new Color32(224, 138, 120, 255);
    static readonly Color32 Info = new Color32(123, 164, 189, 255);
    static readonly Color32 Good = new Color32(143, 174, 134, 255);
    static readonly Color32 Ink = new Color32(27, 29, 24, 255);
    static readonly Color32 Border = new Color32(255, 255, 255, 18);
    static readonly Color32 Track = new Color32(236, 238, 228, 28);

    static HudHandoffBuilder()
    {
        EditorApplication.delayCall += TryProcessRequest;
    }

    [MenuItem("Tools/Slime Defense/Rebuild Dark HUD")]
    public static void BuildFromMenu()
    {
        Build(true);
    }

    // Batch-mode entry point used for verification when the project is closed.
    public static void BuildBatch()
    {
        Build(false);
    }

    static void TryProcessRequest()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene active = SceneManager.GetActiveScene();

        if (active.path != ScenePath && active.isDirty)
        {
            Debug.LogWarning("Dark HUD rebuild is waiting because another unsaved scene is open. " +
                             "Save it, then choose Tools > Slime Defense > Rebuild Dark HUD.");
            return;
        }

        Build(false);
        File.Delete(RequestPath);
        AssetDatabase.Refresh();
    }

    static void Build(bool interactive)
    {
        Scene scene = SceneManager.GetActiveScene();

        if (scene.path != ScenePath)
        {
            if (interactive && scene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Canvas canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            throw new InvalidOperationException("Main scene has no Canvas.");
        }

        Sprite rounded = EnsureRoundedSprite();
        TMP_FontAsset nunito = EnsureFontAsset(NunitoFontPath, NunitoAssetPath);
        TMP_FontAsset mono = EnsureFontAsset(MonoFontPath, MonoAssetPath);

        ConfigureCanvas(canvas);
        DeleteOldHud(canvas.transform);

        GameObject root = UIObject("GameHud", canvas.transform);
        Stretch(root.GetComponent<RectTransform>());

        BuildTopBar(root.transform, rounded, nunito, mono);
        BuildTowerRail(root.transform, rounded, nunito, mono);
        BuildTowerPicker(root.transform, rounded, nunito, mono);
        RestyleEndPanel(canvas.transform, rounded, nunito);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Dark HUD rebuilt from design_handoff_td_hud_dark and saved to Main.unity.");
    }

    static void ConfigureCanvas(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1120f, 700f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    static void DeleteOldHud(Transform canvas)
    {
        HashSet<string> oldNames = new HashSet<string>
        {
            "GameHud", "HUD", "MoneyLabel", "LivesLabel", "WaveLabel",
            "StartWaveButton", "TowerInspectorPanel", "TowerPicker"
        };

        for (int i = canvas.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.GetChild(i);

            if (oldNames.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    static void BuildTopBar(Transform parent, Sprite rounded, TMP_FontAsset body, TMP_FontAsset mono)
    {
        GameObject shadow = ImageObject("TopBarShadow", parent, rounded, new Color32(0, 0, 0, 102));
        SetTopStretch(shadow.GetComponent<RectTransform>(), 28f, 344f, 32f, 64f);

        GameObject bar = ImageObject("TopBar", parent, rounded, Surface);
        SetTopStretch(bar.GetComponent<RectTransform>(), 28f, 344f, 26f, 64f);

        HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 8, 0, 0);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        GameObject moneyGroup = HorizontalGroup("Money", bar.transform, 104f, 64f, 10f);
        Circle("Coin", moneyGroup.transform, 20f, Gold, rounded);
        TMP_Text moneyValue = Label("Value", moneyGroup.transform, "1,240", mono, 22f, Primary,
                                    TextAlignmentOptions.MidlineLeft, true);
        Layout(moneyValue.gameObject, 72f, 32f);

        Divider(bar.transform);

        GameObject livesGroup = HorizontalGroup("Lives", bar.transform, 116f, 64f, 10f);
        GameObject lifeIcon = Circle("LifeIcon", livesGroup.transform, 18f, Danger, rounded);
        GameObject pips = HorizontalGroup("LifePips", livesGroup.transform, 68f, 20f, 4f);
        List<Image> lifePips = new List<Image>();

        for (int i = 0; i < 8; i++)
        {
            GameObject pip = Circle($"Pip{i + 1}", pips.transform, 8f, Danger, rounded);
            lifePips.Add(pip.GetComponent<Image>());
        }

        TMP_Text livesValue = Label("Value", livesGroup.transform, "10", mono, 22f, Primary,
                                    TextAlignmentOptions.MidlineLeft, true);
        Layout(livesValue.gameObject, 42f, 32f);

        Divider(bar.transform);

        GameObject waveGroup = HorizontalGroup("Wave", bar.transform, 138f, 64f, 8f);
        TMP_Text waveCaption = Label("Caption", waveGroup.transform, "WAVE", body, 10f, Secondary,
                                    TextAlignmentOptions.MidlineLeft, true);
        waveCaption.characterSpacing = 2.5f;
        Layout(waveCaption.gameObject, 42f, 22f);
        TMP_Text waveValue = Label("Current", waveGroup.transform, "00", mono, 22f, Primary,
                                  TextAlignmentOptions.MidlineLeft, true);
        Layout(waveValue.gameObject, 34f, 32f);
        TMP_Text waveTotal = Label("Total", waveGroup.transform, "/ 03", mono, 13f, Secondary,
                                  TextAlignmentOptions.MidlineLeft, false);
        Layout(waveTotal.gameObject, 46f, 24f);

        GameObject spacer = UIObject("Spacer", bar.transform);
        LayoutElement spacerLayout = spacer.AddComponent<LayoutElement>();
        spacerLayout.flexibleWidth = 1f;
        spacerLayout.preferredHeight = 1f;

        Button startButton = ButtonObject("StartGameButton", bar.transform, rounded, Gold, GoldHover, Ink);
        Layout(startButton.gameObject, 160f, 44f);
        TMP_Text startLabel = FillLabel("Label", startButton.transform, "START GAME", body, 14f, Ink,
                                       TextAlignmentOptions.Center, true);
        startLabel.characterSpacing = 2.5f;

        Hud hud = bar.AddComponent<Hud>();
        SerializedObject serialized = new SerializedObject(hud);
        Ref(serialized, "moneyLabel", moneyValue);
        Ref(serialized, "livesLabel", livesValue);
        Ref(serialized, "waveLabel", waveValue);
        Ref(serialized, "waveTotalLabel", waveTotal);
        Ref(serialized, "lifePipsRoot", pips);
        Ref(serialized, "lifeIcon", lifeIcon);
        Ref(serialized, "startWaveButton", startButton);
        Ref(serialized, "startWaveLabel", startLabel);
        Ref(serialized, "spawner", UnityEngine.Object.FindAnyObjectByType<WaveSpawner>());

        SerializedProperty pipArray = serialized.FindProperty("lifePips");
        pipArray.arraySize = lifePips.Count;

        for (int i = 0; i < lifePips.Count; i++)
        {
            pipArray.GetArrayElementAtIndex(i).objectReferenceValue = lifePips[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildTowerRail(Transform parent, Sprite rounded, TMP_FontAsset body, TMP_FontAsset mono)
    {
        GameObject shadow = ImageObject("TowerRailShadow", parent, rounded, new Color32(0, 0, 0, 115));
        SetRightRail(shadow.GetComponent<RectTransform>(), 22f, 20f, 288f);

        GameObject rail = ImageObject("TowerInspectorPanel", parent, rounded, Panel);
        SetRightRail(rail.GetComponent<RectTransform>(), 28f, 26f, 288f);
        CanvasGroup railGroup = rail.AddComponent<CanvasGroup>();
        railGroup.alpha = 0f;
        railGroup.interactable = false;
        railGroup.blocksRaycasts = false;
        TowerInspectorPanel inspector = rail.AddComponent<TowerInspectorPanel>();

        GameObject empty = UIObject("EmptyState", rail.transform);
        Stretch(empty.GetComponent<RectTransform>(), 22f, 22f, 22f, 22f);
        TMP_Text emptyTitle = Label("Title", empty.transform, "SELECT A TOWER", body, 14f, Secondary,
                                    TextAlignmentOptions.Center, true);
        SetCentered(emptyTitle.rectTransform, 0f, 24f, 220f, 28f);
        emptyTitle.characterSpacing = 2f;
        TMP_Text emptyHint = Label("Hint", empty.transform,
                                   "Click a placed tower to inspect, upgrade, or sell it.",
                                   body, 12f, new Color32(110, 114, 104, 255),
                                   TextAlignmentOptions.Center, false);
        SetCentered(emptyHint.rectTransform, 0f, -14f, 220f, 56f);
        emptyHint.enableWordWrapping = true;
        empty.SetActive(false);

        GameObject selected = UIObject("SelectedTower", rail.transform);
        Stretch(selected.GetComponent<RectTransform>(), 22f, 22f, 22f, 22f);
        selected.SetActive(false);

        GameObject portrait = ImageObject("Portrait", selected.transform, rounded, Raised);
        SetTopStretch(portrait.GetComponent<RectTransform>(), 0f, 0f, 0f, 96f);
        GameObject portraitAccent = ImageObject("Accent", portrait.transform, rounded, Good);
        SetLeftStretch(portraitAccent.GetComponent<RectTransform>(), 0f, 0f, 4f);
        TMP_Text portraitLabel = FillLabel("TowerType", portrait.transform, "TOWER", body, 16f, Primary,
                                          TextAlignmentOptions.Center, true);
        portraitLabel.characterSpacing = 2f;

        TMP_Text towerName = Label("TowerName", selected.transform, "Tower", body, 20f, Primary,
                                   TextAlignmentOptions.MidlineLeft, true);
        SetTopStretch(towerName.rectTransform, 0f, 54f, 108f, 28f);
        TMP_Text levelLabel = Label("Level", selected.transform, "LV 1", body, 10f, Secondary,
                                   TextAlignmentOptions.MidlineRight, true);
        levelLabel.characterSpacing = 1.5f;
        SetTopRight(levelLabel.rectTransform, 0f, 111f, 52f, 24f);

        StatRow damage = BuildStatRow(selected.transform, "Damage", 150f, Danger, body, mono, rounded);
        StatRow range = BuildStatRow(selected.transform, "Range", 199f, Info, body, mono, rounded);
        StatRow rate = BuildStatRow(selected.transform, "Fire rate", 248f, Good, body, mono, rounded);

        TMP_Text upgradesHeading = Label("UpgradesHeading", selected.transform, "UPGRADES", body, 10f,
                                         Secondary, TextAlignmentOptions.MidlineLeft, true);
        upgradesHeading.characterSpacing = 2.5f;
        SetTopStretch(upgradesHeading.rectTransform, 0f, 0f, 302f, 20f);

        Button upgrade = ButtonObject("UpgradeButton", selected.transform, rounded, Raised, RaisedHover, Primary);
        SetTopStretch(upgrade.GetComponent<RectTransform>(), 0f, 0f, 327f, 78f);
        TMP_Text upgradeTitle = Label("Title", upgrade.transform, "LEVEL 2 UPGRADE", body, 13f, Primary,
                                      TextAlignmentOptions.MidlineLeft, true);
        SetAnchored(upgradeTitle.rectTransform, new Vector2(14f, -13f), new Vector2(150f, 22f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        TMP_Text upgradeEffect = Label("Effect", upgrade.transform, "STATS IMPROVE", mono, 9.5f, Secondary,
                                       TextAlignmentOptions.MidlineLeft, false);
        SetAnchored(upgradeEffect.rectTransform, new Vector2(14f, -42f), new Vector2(170f, 24f),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        Circle("Coin", upgrade.transform, 10f, Gold, rounded);
        RectTransform upgradeCoin = upgrade.transform.Find("Coin").GetComponent<RectTransform>();
        SetAnchored(upgradeCoin, new Vector2(-52f, 0f), new Vector2(10f, 10f),
                    new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(.5f, .5f));
        TMP_Text upgradePrice = Label("Price", upgrade.transform, "100", mono, 14f, Primary,
                                      TextAlignmentOptions.MidlineRight, true);
        SetAnchored(upgradePrice.rectTransform, new Vector2(-12f, 0f), new Vector2(54f, 28f),
                    new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(1f, .5f));

        GameObject footerLine = ImageObject("FooterLine", selected.transform, null,
                                            new Color32(236, 238, 228, 26));
        SetBottomStretch(footerLine.GetComponent<RectTransform>(), 0f, 0f, 61f, 1f);

        Button sell = ButtonObject("SellButton", selected.transform, rounded,
                                   new Color32(40, 43, 35, 0), Danger, Primary);
        SetBottomLeft(sell.GetComponent<RectTransform>(), 0f, 0f, 94f, 40f);
        Image sellImage = sell.GetComponent<Image>();
        Outline outline = sell.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(236, 238, 228, 51);
        outline.effectDistance = new Vector2(1f, -1f);
        TMP_Text sellLabel = FillLabel("Label", sell.transform, "SELL", body, 12f, Primary,
                                      TextAlignmentOptions.Center, true);
        sellLabel.characterSpacing = 2f;

        TMP_Text refundCaption = Label("RefundCaption", selected.transform, "REFUND", body, 10f, Secondary,
                                       TextAlignmentOptions.MidlineRight, true);
        refundCaption.characterSpacing = 1.5f;
        SetBottomRight(refundCaption.rectTransform, 72f, 8f, 66f, 24f);
        GameObject refundCoin = Circle("RefundCoin", selected.transform, 10f, Gold, rounded);
        SetBottomRight(refundCoin.GetComponent<RectTransform>(), 54f, 15f, 10f, 10f);
        TMP_Text refundValue = Label("RefundValue", selected.transform, "0", mono, 15f, Primary,
                                     TextAlignmentOptions.MidlineRight, true);
        SetBottomRight(refundValue.rectTransform, 0f, 6f, 48f, 28f);

        SerializedObject serialized = new SerializedObject(inspector);
        Ref(serialized, "emptyState", empty);
        Ref(serialized, "selectedContent", selected);
        Ref(serialized, "panelShadow", shadow);
        Ref(serialized, "titleLabel", towerName);
        Ref(serialized, "levelLabel", levelLabel);
        Ref(serialized, "portraitLabel", portraitLabel);
        Ref(serialized, "damageValueLabel", damage.Value);
        Ref(serialized, "rangeValueLabel", range.Value);
        Ref(serialized, "fireRateValueLabel", rate.Value);
        Ref(serialized, "damageFill", damage.Fill);
        Ref(serialized, "rangeFill", range.Fill);
        Ref(serialized, "fireRateFill", rate.Fill);
        Ref(serialized, "upgradeButton", upgrade);
        Ref(serialized, "upgradeTitleLabel", upgradeTitle);
        Ref(serialized, "upgradeEffectLabel", upgradeEffect);
        Ref(serialized, "upgradePriceLabel", upgradePrice);
        Ref(serialized, "sellButton", sell);
        Ref(serialized, "sellLabel", sellLabel);
        Ref(serialized, "refundLabel", refundValue);
        Ref(serialized, "placer", UnityEngine.Object.FindAnyObjectByType<TowerPlacer>());
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // Keep transparent SELL backgrounds raycastable.
        sellImage.raycastTarget = true;
    }

    static void BuildTowerPicker(Transform parent, Sprite rounded, TMP_FontAsset body, TMP_FontAsset mono)
    {
        GameObject shadow = ImageObject("BuildShopShadow", parent, rounded, new Color32(0, 0, 0, 102));
        SetBottomLeft(shadow.GetComponent<RectTransform>(), 28f, 20f, 510f, 74f);

        GameObject pickerObject = ImageObject("TowerPicker", parent, rounded, Surface);
        SetBottomLeft(pickerObject.GetComponent<RectTransform>(), 28f, 26f, 510f, 74f);
        HorizontalLayoutGroup layout = pickerObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        string[] paths =
        {
            "Assets/Towers/Tower_Pebble.asset",
            "Assets/Towers/Tower_Splash.asset",
            "Assets/Towers/Tower_Frost.asset"
        };

        TowerPicker picker = pickerObject.AddComponent<TowerPicker>();
        List<Button> buttons = new List<Button>();
        List<TMP_Text> labels = new List<TMP_Text>();
        List<TowerDefinition> definitions = new List<TowerDefinition>();

        foreach (string path in paths)
        {
            TowerDefinition definition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(path);

            if (definition == null)
            {
                continue;
            }

            Button button = ButtonObject(definition.name, pickerObject.transform, rounded,
                                         Raised, RaisedHover, Primary);
            Layout(button.gameObject, 158f, 54f);
            TMP_Text label = FillLabel("Label", button.transform,
                                       $"{definition.DisplayName}\n{definition.Cost}", body, 12f,
                                       Primary, TextAlignmentOptions.Center, true);
            label.lineSpacing = -8f;
            buttons.Add(button);
            labels.Add(label);
            definitions.Add(definition);
        }

        SerializedObject serialized = new SerializedObject(picker);
        Ref(serialized, "placer", UnityEngine.Object.FindAnyObjectByType<TowerPlacer>());
        SerializedProperty options = serialized.FindProperty("options");
        options.arraySize = definitions.Count;

        for (int i = 0; i < definitions.Count; i++)
        {
            SerializedProperty option = options.GetArrayElementAtIndex(i);
            option.FindPropertyRelative("Definition").objectReferenceValue = definitions[i];
            option.FindPropertyRelative("Button").objectReferenceValue = buttons[i];
            option.FindPropertyRelative("Label").objectReferenceValue = labels[i];
        }

        serialized.FindProperty("selectedTint").colorValue = new Color32(111, 129, 95, 255);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void RestyleEndPanel(Transform canvas, Sprite rounded, TMP_FontAsset body)
    {
        Transform panel = canvas.Find("EndOfRunPanel");

        if (panel == null)
        {
            return;
        }

        Image background = panel.GetComponent<Image>();

        if (background != null)
        {
            background.color = new Color32(16, 18, 15, 220);
        }

        CanvasGroup endGroup = panel.GetComponent<CanvasGroup>();

        if (endGroup != null)
        {
            endGroup.alpha = 0f;
            endGroup.interactable = false;
            endGroup.blocksRaycasts = false;
        }

        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (body != null)
            {
                text.font = body;
            }

            text.color = Primary;
            text.raycastTarget = false;
        }

        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            Image image = button.GetComponent<Image>();

            if (image != null)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.color = Gold;
                image.raycastTarget = true;
            }

            foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = Ink;
                label.fontStyle = FontStyles.Bold;
            }
        }
    }

    readonly struct StatRow
    {
        public readonly TMP_Text Value;
        public readonly Image Fill;

        public StatRow(TMP_Text value, Image fill)
        {
            Value = value;
            Fill = fill;
        }
    }

    static StatRow BuildStatRow(Transform parent, string name, float top, Color fillColor,
                                TMP_FontAsset body, TMP_FontAsset mono, Sprite rounded)
    {
        TMP_Text caption = Label(name + "Label", parent, name, body, 12f, Secondary,
                                 TextAlignmentOptions.MidlineLeft, false);
        SetTopStretch(caption.rectTransform, 0f, 70f, top, 20f);
        TMP_Text value = Label(name + "Value", parent, "0", mono, 14f, Primary,
                               TextAlignmentOptions.MidlineRight, true);
        SetTopRight(value.rectTransform, 0f, top - 1f, 68f, 22f);

        GameObject track = ImageObject(name + "Track", parent, rounded, Track);
        SetTopStretch(track.GetComponent<RectTransform>(), 0f, 0f, top + 27f, 4f);
        GameObject fillObject = ImageObject(name + "Fill", track.transform, rounded, fillColor);
        Stretch(fillObject.GetComponent<RectTransform>());
        Image fill = fillObject.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = .5f;
        return new StatRow(value, fill);
    }

    static GameObject HorizontalGroup(string name, Transform parent, float width, float height, float spacing)
    {
        GameObject group = UIObject(name, parent);
        HorizontalLayoutGroup layout = group.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        Layout(group, width, height);
        return group;
    }

    static void Divider(Transform parent)
    {
        GameObject divider = ImageObject("Divider", parent, null, new Color32(236, 238, 228, 36));
        Layout(divider, 1f, 26f);
    }

    static GameObject Circle(string name, Transform parent, float size, Color color, Sprite rounded)
    {
        GameObject circle = ImageObject(name, parent, rounded, color);
        Layout(circle, size, size);
        return circle;
    }

    static Button ButtonObject(string name, Transform parent, Sprite sprite, Color normal,
                               Color highlighted, Color textColor)
    {
        GameObject buttonObject = ImageObject(name, parent, sprite, normal);
        Image image = buttonObject.GetComponent<Image>();
        image.raycastTarget = true;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(.82f, .82f, .82f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(.45f, .45f, .45f, .55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = .08f;
        button.colors = colors;
        return button;
    }

    static GameObject ImageObject(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject gameObject = UIObject(name, parent);
        Image image = gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
        }

        return gameObject;
    }

    static TMP_Text Label(string name, Transform parent, string value, TMP_FontAsset font,
                          float size, Color color, TextAlignmentOptions alignment, bool bold)
    {
        GameObject gameObject = UIObject(name, parent);
        TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    static TMP_Text FillLabel(string name, Transform parent, string value, TMP_FontAsset font,
                              float size, Color color, TextAlignmentOptions alignment, bool bold)
    {
        TMP_Text text = Label(name, parent, value, font, size, color, alignment, bold);
        Stretch(text.rectTransform, 8f, 8f, 4f, 4f);
        return text;
    }

    static GameObject UIObject(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    static void Layout(GameObject gameObject, float width, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();

        if (element == null)
        {
            element = gameObject.AddComponent<LayoutElement>();
        }

        element.preferredWidth = width;
        element.preferredHeight = height;
        element.minWidth = width;
        element.minHeight = height;
    }

    static void Ref(SerializedObject serialized, string property, UnityEngine.Object value)
    {
        SerializedProperty serializedProperty = serialized.FindProperty(property);

        if (serializedProperty != null)
        {
            serializedProperty.objectReferenceValue = value;
        }
    }

    static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
                        float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    static void SetTopStretch(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static void SetBottomStretch(RectTransform rect, float left, float right, float bottom, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, bottom + height);
    }

    static void SetLeftStretch(RectTransform rect, float left, float verticalInset, float width)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, .5f);
        rect.offsetMin = new Vector2(left, verticalInset);
        rect.offsetMax = new Vector2(left + width, -verticalInset);
    }

    static void SetRightRail(RectTransform rect, float right, float verticalInset, float width)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, .5f);
        rect.anchoredPosition = new Vector2(-right, 0f);
        rect.sizeDelta = new Vector2(width, -verticalInset * 2f);
    }

    static void SetTopRight(RectTransform rect, float right, float top, float width, float height)
    {
        SetAnchored(rect, new Vector2(-right, -top), new Vector2(width, height),
                    Vector2.one, Vector2.one, Vector2.one);
    }

    static void SetBottomRight(RectTransform rect, float right, float bottom, float width, float height)
    {
        SetAnchored(rect, new Vector2(-right, bottom), new Vector2(width, height),
                    new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
    }

    static void SetBottomLeft(RectTransform rect, float left, float bottom, float width, float height)
    {
        SetAnchored(rect, new Vector2(left, bottom), new Vector2(width, height),
                    Vector2.zero, Vector2.zero, Vector2.zero);
    }

    static void SetCentered(RectTransform rect, float x, float y, float width, float height)
    {
        SetAnchored(rect, new Vector2(x, y), new Vector2(width, height),
                    new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f));
    }

    static void SetAnchored(RectTransform rect, Vector2 position, Vector2 size,
                            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    static Sprite EnsureRoundedSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);

        if (existing != null)
        {
            return existing;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(RoundedSpritePath));
        const int size = 64;
        const float radius = 16f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + .5f - size * .5f) - (size * .5f - radius);
                float py = Mathf.Abs(y + .5f - size * .5f) - (size * .5f - radius);
                float outside = Mathf.Sqrt(Mathf.Max(px, 0f) * Mathf.Max(px, 0f)
                                           + Mathf.Max(py, 0f) * Mathf.Max(py, 0f));
                float inside = Mathf.Min(Mathf.Max(px, py), 0f);
                float signedDistance = outside + inside - radius;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - signedDistance) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        File.WriteAllBytes(RoundedSpritePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(RoundedSpritePath, ImportAssetOptions.ForceSynchronousImport);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(RoundedSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = 64f;
        importer.spriteBorder = new Vector4(radius, radius, radius, radius);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
    }

    static TMP_FontAsset EnsureFontAsset(string fontPath, string assetPath)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

        if (existing != null)
        {
            return existing;
        }

        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);

        if (font == null)
        {
            Debug.LogWarning($"HUD font missing at {fontPath}; using the TMP default font.");
            return TMP_Settings.defaultFontAsset;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(font);
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        AssetDatabase.CreateAsset(fontAsset, assetPath);

        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
        {
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        foreach (Texture2D atlas in fontAsset.atlasTextures)
        {
            if (atlas != null && !AssetDatabase.Contains(atlas))
            {
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        return fontAsset;
    }
}
#endif
