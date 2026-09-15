using DragonBound.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DragonBound.Tests.EditMode
{
    public sealed class BottomNavigationSelectionTests
    {
        [Test]
        public void ClickingNavigationButtonsLeavesExactlyOneOpaqueSelectionImage()
        {
            var root = new GameObject("BottomNavigation", typeof(RectTransform));
            try
            {
                var ranking = CreateButton(root.transform, "RankingBtn");
                var main = CreateButton(root.transform, "MainBtn");
                var bag = CreateButton(root.transform, "BagBtn");
                var selection = root.AddComponent<BottomNavigationSelection>();
                selection.Configure(
                    ranking, ranking.GetComponent<Image>(),
                    main, main.GetComponent<Image>(),
                    bag, bag.GetComponent<Image>());
                selection.Initialize();

                AssertSelection(ranking, main, bag, main);

                ranking.onClick.Invoke();
                AssertSelection(ranking, main, bag, ranking);

                bag.onClick.Invoke();
                AssertSelection(ranking, main, bag, bag);

                main.onClick.Invoke();
                AssertSelection(ranking, main, bag, main);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<Button>();
        }

        private static void AssertSelection(Button ranking, Button main, Button bag, Button selected)
        {
            foreach (var button in new[] { ranking, main, bag })
            {
                var expected = button == selected
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0f);
                Assert.AreEqual(expected, button.GetComponent<Image>().color, button.name);
                Assert.AreEqual(Selectable.Transition.None, button.transition, button.name);
            }
        }
    }
}
