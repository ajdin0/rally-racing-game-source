using UnityEngine;

public enum RallySurfaceType
{
    Asphalt,
    Gravel,
    Dirt
}

public class RallySurface : MonoBehaviour
{
    public RallySurfaceType surfaceType;
}