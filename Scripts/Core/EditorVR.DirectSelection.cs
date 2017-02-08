#if UNITY_EDITORVR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.EditorVR;
using UnityEngine.Experimental.EditorVR.Modules;
using UnityEngine.Experimental.EditorVR.Utilities;
using UnityEngine.InputNew;

namespace UnityEditor.Experimental.EditorVR
{
	partial class EditorVR : MonoBehaviour
	{
		class DirectSelection
		{
			EditorVR m_EVR;

			public IGrabObjects objectsGrabber { get; set; }

			// Local method use only -- created here to reduce garbage collection
			readonly Dictionary<Transform, DirectSelectionData> m_DirectSelectionResults = new Dictionary<Transform, DirectSelectionData>();
			readonly List<ActionMapInput> m_ActiveStates = new List<ActionMapInput>();

			public DirectSelection(EditorVR evr)
			{
				m_EVR = evr;
			}

			// NOTE: This is for the length of the pointer object, not the length of the ray coming out of the pointer
			public float GetPointerLength(Transform rayOrigin)
			{
				var length = 0f;

				// Check if this is a MiniWorldRay
				MiniWorldRay ray;
				if (m_EVR.m_MiniWorldRays.TryGetValue(rayOrigin, out ray))
					rayOrigin = ray.originalRayOrigin;

				DefaultProxyRay dpr;
				if (m_EVR.m_DefaultRays.TryGetValue(rayOrigin, out dpr))
				{
					length = dpr.pointerLength;

					// If this is a MiniWorldRay, scale the pointer length to the correct size relative to MiniWorld objects
					if (ray != null)
					{
						var miniWorld = ray.miniWorld;

						// As the miniworld gets smaller, the ray length grows, hence localScale.Inverse().
						// Assume that both transforms have uniform scale, so we just need .x
						length *= miniWorld.referenceTransform.TransformVector(miniWorld.miniWorldTransform.localScale.Inverse()).x;
					}
				}

				return length;
			}

			public Dictionary<Transform, DirectSelectionData> GetDirectSelection()
			{
				m_DirectSelectionResults.Clear();
				m_ActiveStates.Clear();

				var directSelection = objectsGrabber;
				m_EVR.ForEachProxyDevice((deviceData) =>
				{
					var rayOrigin = deviceData.rayOrigin;
					var input = deviceData.directSelectInput;
					var obj = GetDirectSelectionForRayOrigin(rayOrigin, input);
					if (obj && !obj.CompareTag(kVRPlayerTag))
					{
						m_ActiveStates.Add(input);
						m_DirectSelectionResults[rayOrigin] = new DirectSelectionData
						{
							gameObject = obj,
							node = deviceData.node,
							input = input
						};
					}
					else if (directSelection != null && directSelection.GetHeldObjects(rayOrigin) != null)
					{
						m_ActiveStates.Add(input);
					}
				});

				foreach (var ray in m_EVR.m_MiniWorldRays)
				{
					var rayOrigin = ray.Key;
					var miniWorldRay = ray.Value;
					var input = miniWorldRay.directSelectInput;
					var go = GetDirectSelectionForRayOrigin(rayOrigin, input);
					if (go != null)
					{
						m_ActiveStates.Add(input);
						m_DirectSelectionResults[rayOrigin] = new DirectSelectionData
						{
							gameObject = go,
							node = ray.Value.node,
							input = input
						};
					}
					else if (miniWorldRay.dragObjects != null
						|| (directSelection != null && directSelection.GetHeldObjects(rayOrigin) != null))
					{
						m_ActiveStates.Add(input);
					}
				}

				// Only activate direct selection input if the cone is inside of an object, so a trigger press can be detected,
				// and keep it active if we are dragging
				m_EVR.ForEachProxyDevice((deviceData) =>
				{
					var input = deviceData.directSelectInput;
					input.active = m_ActiveStates.Contains(input);
				});

				return m_DirectSelectionResults;
			}

			GameObject GetDirectSelectionForRayOrigin(Transform rayOrigin, ActionMapInput input)
			{
				if (m_EVR.m_IntersectionModule)
				{
					var tester = rayOrigin.GetComponentInChildren<IntersectionTester>();

					var renderer = m_EVR.m_IntersectionModule.GetIntersectedObjectForTester(tester);
					if (renderer)
						return renderer.gameObject;
				}
				return null;
			}

			public bool CanGrabObject(GameObject selection, Transform rayOrigin)
			{
				if (selection.CompareTag(kVRPlayerTag) && !m_EVR.m_MiniWorldRays.ContainsKey(rayOrigin))
					return false;

				return true;
			}

			public void OnObjectGrabbed(GameObject selection)
			{
				// Detach the player head model so that it is not affected by its parent transform
				if (selection.CompareTag(kVRPlayerTag))
					selection.transform.parent = null;
			}

			public void OnObjectsDropped(Transform[] grabbedObjects, Transform rayOrigin)
			{
				foreach (var grabbedObject in grabbedObjects)
				{
					// Dropping the player head updates the viewer pivot
					if (grabbedObject.CompareTag(kVRPlayerTag))
						m_EVR.StartCoroutine(UpdateViewerPivot(grabbedObject));
					else if (m_EVR.IsOverShoulder(rayOrigin) && !m_EVR.m_MiniWorldRays.ContainsKey(rayOrigin))
						m_EVR.DeleteSceneObject(grabbedObject.gameObject);
				}
			}
		}
	}
}
#endif
