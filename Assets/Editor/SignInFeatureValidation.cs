using System;
using DragonBound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SignInFeatureValidation
{
    private static readonly string MainScenePath = UiVariantProjectPaths.V1Scene("Main");

    [MenuItem("DragonBound/Validation/Validate Sign-In Feature")]
    public static void Run()
    {
        ValidateRewardSchedule();
        ValidateCycleRules();
        ValidateTextFormatting();
        ValidateMainScene();
        Debug.Log("Sign-in feature validation passed.");
    }

    private static void ValidateRewardSchedule()
    {
        RequireGold(1, 40);
        RequireGold(2, 60);
        RequireRune(3, RuneRarity.Epic);
        RequireGold(4, 80);
        RequireGold(5, 100);
        RequireGold(6, 160);
        RequireRune(7, RuneRarity.Legendary);
    }

    private static void ValidateCycleRules()
    {
        Require(LocalSignInGateway.ResolveCycleDay(0, true) == 1, "First claim must be Day One.");
        Require(LocalSignInGateway.ResolveCycleDay(2, true) == 3, "Missed dates must not reset progress.");
        Require(LocalSignInGateway.ResolveCycleDay(7, true) == 1, "Day Seven must loop to Day One.");
        Require(LocalSignInGateway.ResolveCycleDay(3, false) == 3, "Claimed day must remain visible.");
    }

    private static void ValidateTextFormatting()
    {
        Require(MainSignInController.FormatDay(1) == "Day One", "Day One text format is incorrect.");
        Require(MainSignInController.FormatDay(7) == "Day Seven", "Day Seven text format is incorrect.");
        Require(
            MainSignInController.FormatReward(LocalSignInGateway.CreatePreview(1)) == "Gold：40",
            "Gold reward text format is incorrect.");
    }

    private static void ValidateMainScene()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        try
        {
            MainSignInController controller = FindComponentInScene<MainSignInController>(scene);
            Require(controller != null, "MainPanel is missing MainSignInController.");
            Transform panel = controller.transform.FindUi("SignPanel");
            Transform imageRoot = panel != null ? panel.FindUi("Image") : null;
            Transform content = imageRoot != null ? imageRoot.FindUi("ContentCon") : null;
            Require(panel != null, "MainPanel/SignPanel is missing.");
            Require(!panel.gameObject.activeSelf, "SignPanel must be inactive before status loads.");
            RequireComponent<Button>(controller.transform, "SignBtn");
            RequireComponent<Button>(imageRoot, "closeBtn");
            for (int index = 0; index < 6; index++)
            {
                string name = index == 0 ? "Image" : "Image (" + index + ")";
                RequireComponent<Button>(content, name);
                RequireComponent<Image>(content, name);
            }
            RequireComponent<Button>(imageRoot, "Image (6)");
            RequireComponent<Image>(imageRoot, "Image (6)");
            Require(
                UiAssets.Load<Sprite>("Main/Signin/Today") != null,
                "Today.png must be imported as a Sprite.");
            Require(
                UiAssets.Load<Sprite>("Main/Signin/other") != null,
                "other.png must be imported as a Sprite.");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null) return component;
        }
        return null;
    }

    private static void RequireComponent<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent != null ? parent.FindUi(childName) : null;
        Require(child != null, childName + " is missing from the sign-in hierarchy.");
        Require(child.GetComponent<T>() != null, childName + " has the wrong component type.");
    }

    private static void RequireGold(int day, int amount)
    {
        SignInReward reward = LocalSignInGateway.CreatePreview(day);
        Require(reward.Type == SignInRewardType.Gold, "Day " + day + " must reward Gold.");
        Require(reward.Amount == amount, "Day " + day + " Gold amount is incorrect.");
    }

    private static void RequireRune(int day, RuneRarity rarity)
    {
        SignInReward reward = LocalSignInGateway.CreatePreview(day);
        Require(reward.Type == SignInRewardType.CompleteRune, "Day " + day + " must reward a complete Rune.");
        Require(reward.RuneRarity == rarity, "Day " + day + " Rune rarity is incorrect.");
        Require(reward.Amount == 1, "Day " + day + " Rune amount must be one.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
