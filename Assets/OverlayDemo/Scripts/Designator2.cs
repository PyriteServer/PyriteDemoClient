using UnityEngine;
using System.Collections;

public class Designator2 : MonoBehaviour
{
    public float TurnRate = 45;
    public float MoveRate = 0.5f;
    public float Height = 20f;
    public float Scale = 3f;

    private Vector3 BasePos;
    private Vector3 Destination;
    private Vector3 currentDest;
    private bool direction;
    private float basePosY;
    private float scaleLen;
    private float scale;

    void Awake()
    {
        BasePos = transform.position;        
        Destination = transform.position;
        Destination.y += Height;
        currentDest = Destination;

        basePosY = BasePos.y;    
    }

    void Update()
    {
        transform.Rotate(Vector3.up * Time.deltaTime * TurnRate);
        transform.position = Vector3.Lerp(transform.position, currentDest, MoveRate * Time.deltaTime);        
        scale = (transform.position.y - basePosY) * ((Scale - 1) / Height) + 1;
        transform.localScale = new Vector3(scale, scale, scale);
        if(direction)
        {
            if ((Destination - transform.position).sqrMagnitude  < 0.5f)
            {
                direction = false;
                currentDest = BasePos;
            }
        }
        else
        {
            if ((BasePos - transform.position).sqrMagnitude < 0.5f)
            {
                direction = true;
                currentDest = Destination;
            }
        }
    }
}
