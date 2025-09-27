// ImageTargetData.cs
using UnityEngine;

[System.Serializable]
public class ImageTargetData
{
    [Tooltip("The exact name of the image in your Reference Image Library.")]
    public string imageName;

    [Tooltip("Drag the corresponding Anchor Point GameObject from your environment hierarchy here.")]
    public Transform anchorPointInMap;
}