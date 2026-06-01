using UnityEngine;
using UnityEditor;

namespace VibeUnity.Editor
{
    /// <summary>
    /// Primitive object creation functionality for Vibe Unity
    /// </summary>
    public static class VibeUnityPrimitives
    {
        /// <summary>
        /// Creates a cube primitive at the specified position
        /// </summary>
        public static bool CreateCube(string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            return CreatePrimitive(name, PrimitiveType.Cube, position, rotation, scale);
        }
        
        /// <summary>
        /// Creates a sphere primitive at the specified position
        /// </summary>
        public static bool CreateSphere(string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            return CreatePrimitive(name, PrimitiveType.Sphere, position, rotation, scale);
        }
        
        /// <summary>
        /// Creates a plane primitive at the specified position
        /// </summary>
        public static bool CreatePlane(string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            return CreatePrimitive(name, PrimitiveType.Plane, position, rotation, scale);
        }
        
        /// <summary>
        /// Creates a cylinder primitive at the specified position
        /// </summary>
        public static bool CreateCylinder(string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            return CreatePrimitive(name, PrimitiveType.Cylinder, position, rotation, scale);
        }
        
        /// <summary>
        /// Creates a capsule primitive at the specified position
        /// </summary>
        public static bool CreateCapsule(string name, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            return CreatePrimitive(name, PrimitiveType.Capsule, position, rotation, scale);
        }
        
        /// <summary>
        /// Creates a primitive GameObject of the specified type
        /// </summary>
        /// <param name="objectName">Name for the GameObject</param>
        /// <param name="primitiveType">Type of primitive to create</param>
        /// <param name="position">World position</param>
        /// <param name="rotation">Euler angles rotation</param>
        /// <param name="scale">Scale vector</param>
        /// <returns>True if primitive was created successfully</returns>
        public static bool CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            try
            {
                // Create the primitive GameObject
                GameObject primitiveObject = GameObject.CreatePrimitive(primitiveType);
                primitiveObject.name = objectName;
                primitiveObject.transform.position = position;
                primitiveObject.transform.eulerAngles = rotation;
                primitiveObject.transform.localScale = scale;
                
                Debug.Log($"[VibeUnity] ✅ SUCCESS: Created {primitiveType} '{objectName}' at {position}");
                
                // Mark scene as dirty before saving
                VibeUnityScenes.MarkActiveSceneDirty();
                
                // Save the scene after successful creation
                VibeUnityScenes.SaveActiveScene(false, true); // Force save even during batch processing
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibeUnity] Failed to create {primitiveType}: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Creates a primitive GameObject with logging support
        /// </summary>
        public static bool CreatePrimitiveWithLogging(string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 rotation, Vector3 scale, System.Text.StringBuilder logCapture, string typeName, string color = null)
        {
            try
            {
                logCapture.AppendLine($"{typeName} Name: {objectName}");
                logCapture.AppendLine($"Position: {position}");
                logCapture.AppendLine($"Scale: {scale}");

                // Create the primitive GameObject
                GameObject primitiveObject = GameObject.CreatePrimitive(primitiveType);
                primitiveObject.name = objectName;
                primitiveObject.transform.position = position;
                primitiveObject.transform.rotation = Quaternion.Euler(rotation);
                primitiveObject.transform.localScale = scale;

                // Apply color if specified
                if (!string.IsNullOrEmpty(color))
                {
                    if (ColorUtility.TryParseHtmlString(color, out Color parsedColor))
                    {
                        Renderer renderer = primitiveObject.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.material = new Material(renderer.sharedMaterial);
                            renderer.material.color = parsedColor;
                            logCapture.AppendLine($"   └─ Color: {color}");
                        }
                    }
                    else
                    {
                        logCapture.AppendLine($"⚠️ Warning: Could not parse color: {color}");
                    }
                }

                logCapture.AppendLine($"✅ {typeName} created successfully: {objectName}");
                logCapture.AppendLine($"   └─ Position: {primitiveObject.transform.position}");
                logCapture.AppendLine($"   └─ Has Collider: {primitiveObject.GetComponent<Collider>() != null}");
                logCapture.AppendLine($"   └─ Has Renderer: {primitiveObject.GetComponent<Renderer>() != null}");

                // Mark scene as dirty to ensure changes are saved
                VibeUnityScenes.MarkActiveSceneDirty();

                return true;
            }
            catch (System.Exception e)
            {
                logCapture.AppendLine($"❌ Exception in add-{typeName.ToLower()}: {e.Message}");
                logCapture.AppendLine($"Stack Trace: {e.StackTrace}");
                return false;
            }
        }
    }
}