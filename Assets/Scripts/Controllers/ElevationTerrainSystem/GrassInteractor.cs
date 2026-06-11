using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A component that registers the GameObject as a grass interactor.
/// Any moving unit or object with this component will dynamically bend the compute-shader grass.
/// </summary>
public class GrassInteractor : MonoBehaviour
{
    private static readonly List<GrassInteractor> _activeInteractors = new List<GrassInteractor>();
    
    /// <summary>
    /// Static list of all active interactors in the scene.
    /// </summary>
    public static List<GrassInteractor> ActiveInteractors => _activeInteractors;

    [Tooltip("Radius of the bending effect around this object.")]
    [SerializeField] private float _radius = 1.5f;

    /// <summary>
    /// The radius of the bending deformation caused by this interactor.
    /// </summary>
    public float Radius => _radius;

    private void OnEnable()
    {
        _activeInteractors.Add(this);
    }

    private void OnDisable()
    {
        _activeInteractors.Remove(this);
    }
}
