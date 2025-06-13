using UnityEngine;

public class ParallaxController : MonoBehaviour
{
    private Vector2 startPos;
    private Vector2 length;
    public float parallaxEffectX = 0.5f;
    public float parallaxEffectY = 0.5f;
    
    public float parallaxStrength = 0.5f;
    private Material material;
    private Vector2 offset;

    void Start()
    {
        startPos = transform.position;
        length = GetComponent<MeshRenderer>().bounds.size;
        material = GetComponent<MeshRenderer>().material;
        offset = Vector2.zero;
    }

    void Update()
    {
        // Calculate parallax offset based on object's position
        float tempX = (transform.position.x * (1 - parallaxEffectX));
        float tempY = (transform.position.y * (1 - parallaxEffectY));
        
        float distX = (transform.position.x * parallaxEffectX) * parallaxStrength;
        float distY = (transform.position.y * parallaxEffectY) * parallaxStrength;

        // Update offset
        offset.x = distX;
        offset.y = distY;

        // Apply offset to material
        material.mainTextureOffset = offset;

        // Handle wrapping
        if (tempX > startPos.x + length.x) startPos.x += length.x;
        else if (tempX < startPos.x - length.x) startPos.x -= length.x;

        if (tempY > startPos.y + length.y) startPos.y += length.y;
        else if (tempY < startPos.y - length.y) startPos.y -= length.y;
    }
} 