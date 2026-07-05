using UnityEngine;

namespace Ursa.UI
{
    /// <summary>
    /// Marks a scene Canvas that should be managed by Ursa's shared UI camera.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class UrsaUICanvas : MonoBehaviour
    {
    }
}
