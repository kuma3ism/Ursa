using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Ursa;

namespace Ursa.Tests
{
    [TestFixture]
    public class UrsaCoreTests
    {
        private class MockSceneManager : ISceneManager
        {
            public Task ResetAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour => Task.CompletedTask;
            public Task PushAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour => Task.CompletedTask;
            public Task PopAsync() => Task.CompletedTask;
            public Task ReplaceAsync<TScene>(ISceneParameter parameter) where TScene : MonoBehaviour => Task.CompletedTask;
            public bool IsTopScene(UnityEngine.SceneManagement.Scene scene) => false;
            public bool IsTransitioning => false;
            public Task<TScene> CreateSceneAsync<TScene>(ISceneParameter parameter = null) where TScene : MonoBehaviour => Task.FromResult<TScene>(null);
            public Task PushInstanceAsync(UnityEngine.SceneManagement.Scene scene) => Task.CompletedTask;
            public Task ReplaceInstanceAsync(UnityEngine.SceneManagement.Scene scene) => Task.CompletedTask;
            public Task<TResult> OpenResultAsync<TScene, TParam, TResult>(TParam parameter)
                where TScene : Ursa.Scenes.SceneBaseWithResult<TParam, TResult>
                where TParam : ISceneParameter => Task.FromResult(default(TResult));
        }

        [SetUp]
        public void SetUp()
        {
            // Ensure state is clean before each test
            UrsaCore.Dispose();
        }

        [TearDown]
        public void TearDown()
        {
            UrsaCore.Dispose();
        }

        [Test]
        public void IsReady_InitialState_IsFalse()
        {
            Assert.That(UrsaCore.IsReady, Is.False);
        }

        [Test]
        public void Scene_WhenNotInitialized_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => {
                var s = UrsaCore.Scene;
            });
        }

        [Test]
        public void Initialize_WithValidManager_SetsSceneAndIsReady()
        {
            var mock = new MockSceneManager();
            UrsaCore.Initialize(mock);

            Assert.That(UrsaCore.IsReady, Is.True);
            Assert.That(UrsaCore.Scene, Is.SameAs(mock));
        }

        [Test]
        public void Initialize_WithNull_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => {
                UrsaCore.Initialize(null);
            });
        }

        [Test]
        public void Dispose_ResetsState()
        {
            var mock = new MockSceneManager();
            UrsaCore.Initialize(mock);
            Assert.That(UrsaCore.IsReady, Is.True);

            UrsaCore.Dispose();

            Assert.That(UrsaCore.IsReady, Is.False);
            Assert.Throws<InvalidOperationException>(() => {
                var s = UrsaCore.Scene;
            });
        }

        [Test]
        public void Initialize_Twice_AllowsReinitialization()
        {
            var mock1 = new MockSceneManager();
            var mock2 = new MockSceneManager();

            UrsaCore.Initialize(mock1);
            Assert.That(UrsaCore.Scene, Is.SameAs(mock1));

            UrsaCore.Initialize(mock2);
            Assert.That(UrsaCore.Scene, Is.SameAs(mock2));
        }
    }
}
