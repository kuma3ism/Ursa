using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Ursa.Scenes;

namespace Ursa.Tests
{
    [TestFixture]
    public class SceneBaseWithResultTests
    {
        private class TestParameter : ISceneParameter { }

        private class TestScene : SceneBaseWithResult<TestParameter, int>
        {
        }

        private GameObject _gameObject;
        private TestScene _scene;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestSceneObject");
            _scene = _gameObject.AddComponent<TestScene>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void WaitForResultAsync_WhenCalledBeforeOpenAsync_ThrowsInvalidOperationException()
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                var task = _scene.WaitForResultAsync();
            });

            Assert.That(exception.Message, Is.EqualTo("[Ursa] WaitForResultAsync() は OpenAsync() を呼んだ後に使用してください。"));
        }
    }
}
