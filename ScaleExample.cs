using System;
using UnityEngine;
public class ScaleExample : MonoBehaviour, IPinchHandler{
    [Header("Object Min Max")]
    public float ObjectHeight;
    public  float   MinObjectHeight;
    public  float   MaxObjectHeight;
    public  float   ScaleSpeed = 10f;
    
    private float   ScreenPercentOfDifference;
    private float   ScaledDifference;
    private float   ResultedHeight;
    private Vector2 PositionChange = Vector2.zero;
    
    public void OnBeginPinch(PinchEventData eventData){
    }

    public void OnPinch(PinchEventData eventData){
        PositionChange            =  eventData.GetPositionChange();
        ScaleSpeed                *= Math.Sign(eventData.FingerDistanceChange);
        ScreenPercentOfDifference =  PositionChange.x/Screen.width + PositionChange.y/Screen.height;
        ResultedHeight            =  ObjectHeight + ScreenPercentOfDifference * ScaleSpeed;

        if (ResultedHeight < MinObjectHeight || ResultedHeight > MaxObjectHeight) return;

        ObjectHeight = ResultedHeight;
    }

    public void OnEndPinch(PinchEventData eventData){
    }
}
