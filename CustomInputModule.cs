using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
public abstract class PinchEventData : BaseEventData{
    protected PinchEventData(EventSystem eventSystem) : base(eventSystem){
    }

    public abstract Vector2 GetCurrentPinchPos(int index);

    public virtual float currentFingerDistance{
        get{ return Vector2.Distance(GetCurrentPinchPos(0), GetCurrentPinchPos(1)); }
    }

    public abstract Vector2 GetLastPinchPos(int index);

    public virtual float lastFingerDistance{
        get{ return Vector2.Distance(GetLastPinchPos(0), GetLastPinchPos(1)); }
    }

    public virtual float FingerDistanceChange{
        get{ return lastFingerDistance - currentFingerDistance; }
    }

    public abstract Vector2 GetPositionChange();
}

public interface IPinchHandler : IEventSystemHandler{
    void OnBeginPinch(PinchEventData eventData);

    void OnPinch(PinchEventData eventData);

    void OnEndPinch(PinchEventData eventData);
}

public static class CustomEventHandlers{
    public static readonly ExecuteEvents.EventFunction<IPinchHandler> beginPinchHandler = ExecuteBeginPinch;
    public static readonly ExecuteEvents.EventFunction<IPinchHandler> pinchHandler      = ExecutePinch;
    public static readonly ExecuteEvents.EventFunction<IPinchHandler> endPinchHandler   = ExecuteEndPinch;

    private static void ExecuteBeginPinch(IPinchHandler handler, BaseEventData eventData){
        handler.OnBeginPinch(ExecuteEvents.ValidateEventData<PinchEventData>(eventData));
    }

    private static void ExecutePinch(IPinchHandler handler, BaseEventData eventData){
        handler.OnPinch(ExecuteEvents.ValidateEventData<PinchEventData>(eventData));
    }

    private static void ExecuteEndPinch(IPinchHandler handler, BaseEventData eventData){
        handler.OnEndPinch(ExecuteEvents.ValidateEventData<PinchEventData>(eventData));
    }
}

public class CustomInputModule : StandaloneInputModule{
    public float pinchThreshold;

    public class PinchFinger{
        public int     fingerId;
        public Vector2 position;
        public Vector2 lastPosition;
    }

    public           GameObject[]      m_pinchTarget;
    private readonly List<PinchFinger> m_pinchFingers = new List<PinchFinger>(2);
    private          float             m_lastPinchDistance;
    private          bool              m_pinching;
    private          PinchEventData    m_pinchEventData;

    private class PinchEventDataImpl : PinchEventData{
        private readonly CustomInputModule m_inputModule;

        public PinchEventDataImpl(EventSystem eventSystem, CustomInputModule module) : base(eventSystem){
            m_inputModule = module;
        }

        public override Vector2 GetCurrentPinchPos(int index){
            return m_inputModule.m_pinchFingers[index].position;
        }

        public override Vector2 GetLastPinchPos(int index){
            return m_inputModule.m_pinchFingers[index].lastPosition;
        }

        public override Vector2 GetPositionChange(){
            Vector2 firstFingerChange = GetCurrentPinchPos(0) - GetLastPinchPos(0);
            firstFingerChange.x = Math.Abs(firstFingerChange.x);
            firstFingerChange.y = Math.Abs(firstFingerChange.y);
            Vector2 SecondFingerChange = GetCurrentPinchPos(1) - GetLastPinchPos(1);
            SecondFingerChange.x = Math.Abs(SecondFingerChange.x);
            SecondFingerChange.y = Math.Abs(SecondFingerChange.y);
            return firstFingerChange + SecondFingerChange;
        }
    }

    private class TouchEventData{
        public Touch            touch;
        public bool             released;
        public bool             pressed;
        public PointerEventData pointer;
    }

    private readonly List<TouchEventData>  m_touchEventData = new List<TouchEventData>();
    private readonly Stack<TouchEventData> m_touchEventPool = new Stack<TouchEventData>();

    protected override void Awake(){
        base.Awake();
        m_InputOverride = GetComponent<BaseInput>();
        m_pinchTarget = FindObjectsOfType<MonoBehaviour>()
                        .Where(el => el.GetComponent<IPinchHandler>() != null)
                        .Select(el => el.gameObject)
                        .ToArray();
    }

    protected override void OnEnable(){
        base.OnEnable();
        m_pinchEventData = new PinchEventDataImpl(eventSystem, this);
    }

    public override void DeactivateModule(){
        base.DeactivateModule();
        EndPinch();
        m_pinchFingers.Clear();
        m_pinchTarget = null;
    }

    private void EndPinch(){
        if (m_pinching){
            m_pinching = false;
            for (int i = 0; i < m_pinchTarget.Length; i++){
                ExecuteEvents.Execute(m_pinchTarget[i], m_pinchEventData, CustomEventHandlers.endPinchHandler);
            }
        }
    }

    public override void Process(){
        bool usedEvent = SendUpdateEventToSelectedObject();

        if (eventSystem.sendNavigationEvents){
            if (!usedEvent)
                usedEvent |= SendMoveEventToSelectedObject();

            if (!usedEvent)
                SendSubmitEventToSelectedObject();
        }

        // touch needs to take precedence because of the mouse emulation layer
        if (!ProcessTouchEvents())
            ProcessMouseEvent();
    }

    private bool ProcessTouchEvents(){
        bool pinchMoved = false;

        for (int i = 0; i < input.touchCount; ++i){
            Touch touch = input.GetTouch(i);

            if (touch.type == TouchType.Indirect) continue;

            bool released;
            bool pressed;
            var  pointer = GetTouchPointerEventData(touch, out pressed, out released);

            var eventData = GetTouchEventData();
            eventData.touch    = touch;
            eventData.released = released;
            eventData.pressed  = pressed;
            eventData.pointer  = pointer;
            m_touchEventData.Add(eventData);

            int pinchIndex = m_pinchFingers.FindIndex(x => x.fingerId == touch.fingerId);
            if (released && pinchIndex != -1){


                EndPinch();
                m_pinchFingers.RemoveAt(pinchIndex);
                if (m_pinchFingers.Count == 0){
                    //m_pinchTarget = null;
                }
            }
            else if (pressed && pinchIndex == -1){
                m_pinchFingers.Add(new PinchFinger{
                    fingerId = touch.fingerId, lastPosition = touch.position, position = touch.position,
                });
                if (m_pinchFingers.Count == 2){
                    m_lastPinchDistance = Vector2.Distance(m_pinchFingers[0].position, m_pinchFingers[1].position);
                }
            }
            else if (pinchIndex != -1){
                if (m_pinchFingers[pinchIndex].lastPosition != touch.position){
                    //Debug.Log("update pinch finger position: " + input.fingerId + " " + input.position);

                    m_pinchFingers[pinchIndex].lastPosition = m_pinchFingers[pinchIndex].position;
                    m_pinchFingers[pinchIndex].position     = touch.position;
                    pinchMoved                              = true;
                }
            }
        }

        if (m_pinchFingers.Count == 2){
            if (!m_pinching && pinchMoved){
                var moveDistance = Vector2.Distance(m_pinchFingers[0].position, m_pinchFingers[1].position) -
                                   m_lastPinchDistance;
                if (Mathf.Abs(moveDistance) >= pinchThreshold){
                    m_pinching = true;
                    for (int i = 0; i < m_pinchTarget.Length; i++){
                        ExecuteEvents.Execute(m_pinchTarget[i], m_pinchEventData, CustomEventHandlers.beginPinchHandler);
                    }
                 

                    for (int i = 0; i < m_touchEventData.Count; ++i){
                        var eventData = m_touchEventData[i];
                        CancelTouchPress(eventData.pointer);
                        RemovePointerData(eventData.pointer);
                    }
                }
            }

            if (m_pinching && pinchMoved){
                for (int i = 0; i < m_pinchTarget.Length; i++){
                    ExecuteEvents.Execute(m_pinchTarget[i], m_pinchEventData, CustomEventHandlers.pinchHandler);
                }
            }
        }

        if (!m_pinching){
            for (int i = 0; i < m_touchEventData.Count; ++i){
                var eventData = m_touchEventData[i];
                ProcessTouchPress(eventData.pointer, eventData.pressed, eventData.released);

                if (!eventData.released){
                    ProcessMove(eventData.pointer);
                    ProcessDrag(eventData.pointer);
                }
                else
                    RemovePointerData(eventData.pointer);
            }
        }

        ReleaseAllTouchEventData();

        return input.touchCount > 0;
    }

    private TouchEventData GetTouchEventData(){
        if (m_touchEventPool.Count == 0){
            return new TouchEventData();
        }

        return m_touchEventPool.Pop();
    }

    private void ReleaseAllTouchEventData(){
        for (int i = 0; i < m_touchEventData.Count; ++i){
            m_touchEventPool.Push(m_touchEventData[i]);
        }

        m_touchEventData.Clear();
    }

    private void CancelTouchPress(PointerEventData pointerEvent){
        ExecuteEvents.Execute(pointerEvent.pointerPress, pointerEvent, ExecuteEvents.pointerUpHandler);

        pointerEvent.eligibleForClick = false;
        pointerEvent.pointerPress     = null;
        pointerEvent.rawPointerPress  = null;

        if (pointerEvent.pointerDrag != null && pointerEvent.dragging)
            ExecuteEvents.Execute(pointerEvent.pointerDrag, pointerEvent, ExecuteEvents.endDragHandler);

        pointerEvent.dragging    = false;
        pointerEvent.pointerDrag = null;

        // send exit events as we need to simulate this on touch up on touch device
        ExecuteEvents.ExecuteHierarchy(pointerEvent.pointerEnter, pointerEvent, ExecuteEvents.pointerExitHandler);
        pointerEvent.pointerEnter = null;
    }

    private void Reset(){
        pinchThreshold = GetComponent<EventSystem>().pixelDragThreshold;
    }
}