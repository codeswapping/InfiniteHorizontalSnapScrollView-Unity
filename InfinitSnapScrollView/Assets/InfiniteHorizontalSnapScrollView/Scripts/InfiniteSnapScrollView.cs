using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace InfiniteHorizontalSnapScrollView.Scripts
{
    [RequireComponent(typeof(Mask))]
    [RequireComponent(typeof(Image))]
    public class InfiniteSnapScrollView : MonoBehaviour
    {
        #region PUBLIC_VERIABLES

        [Header("Swipe")]
        [FormerlySerializedAs("_timeThreshHold"),
         Tooltip("Determines time limit for distance threshold for swipe detection")]
        public float timeThreshHold = 0.5f;

        [FormerlySerializedAs("_distanceThreshHold"),
         Tooltip("Determine distance to pass from one position to another for swipe detection")]
        public float distanceThreshHold = 1f;

        [Space(20)]
        [Header("Snap")]
        [FormerlySerializedAs("AnimationTime"), Tooltip("Determine snap time after drag or swipe effect is finished")]
        public float animationTime = 0.5f;

        [FormerlySerializedAs("_snapLimit")] public float snapLimit = 1f;

        [Space(20)]
        [Header("Scrolling")]
        [Tooltip("Determines scroll sensitivity, the higher the value, more force to scroll")]
        public float scrollSensitivity = 5;

        [FormerlySerializedAs("_isAutoScroll"), Tooltip("Enables or disables auto scrolling (one item at a time)")]
        public bool isAutoScroll;

        [FormerlySerializedAs("_autoScrollDelay"), Tooltip("Delay in seconds")]
        public float autoScrollDelay = 2f;

        [FormerlySerializedAs("_enableScrolling"),
         Tooltip(
             "Enables or disables scrolling behavior, if false, then only one item at a time will be scrolled on swipe")]
        public bool enableScrolling;

        [FormerlySerializedAs("_scrollSpeed")] public float scrollSpeed = 20f;
        [FormerlySerializedAs("_stopSpeed")] public float stopSpeed = 10f;

        public bool SetScrolling
        {
            get { return _isScrollable; }
            set { _isScrollable = value;
                if (value)
                {
                    _isAutoScrollStopTemp = false;
                }
                else
                {
                    _isAutoScrollStopTemp = true;
                    StopAutoScroll();
                    StopAllCoroutines();
                }
            }
        }

        #endregion

        #region PRIVATE_VARIABLES
        private RectTransform _contentContainer;
        private bool _isScrollable;
        private bool _isScrollableDefault;
        private Vector2 _initPos, _startPos, _endPos;
        private float _startTime, _endTime;
        private float _itemWidth;
        private float _maxXPos;
        private float _swipeSpeed = 0.1f;
        private bool _isAutoScrollRunning;
        private bool _isAutoScrollStopTemp;
        private int _currentIndex;
        private bool _isMouseScrolling;
        private Transform _closeOne;
        private float _autoScrollEnableTime;
        #endregion

        #region UNITY_METHODS

        private void OnEnable()
        {
            OnUpdateLayout();
            if (_contentContainer == null || _contentContainer.childCount <= 1) return;
            _isScrollableDefault = true;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            StopAutoScroll();
        }

        private void Update()
        {
            CheckTouchInput();
            CheckMouseInput();
            if (isAutoScroll && _contentContainer.childCount > 1)
            {
                if (_autoScrollEnableTime <= 0)
                {
                    if (_isAutoScrollRunning) return;
                    StartAutoScroll();
                }
                else
                {
                    _autoScrollEnableTime -= Time.deltaTime;
                }
            }
            else
            {
                if (!_isAutoScrollRunning) return;
                StopAutoScroll();
            }
        }

        #endregion

        #region PRIVATE_METHODS

        /// <summary>
        /// Checking Mouse Input, We need EventTriggers for detection of OnPointerUp and OnPointerDown as base for this method to work.
        /// </summary>
        private void CheckMouseInput()
        {
            if (!_isScrollableDefault || !_isMouseScrolling) return;
            if (isAutoScroll)
            {
                _autoScrollEnableTime = 5f;
                if (_isAutoScrollRunning)
                {
                    StopAutoScroll();
                    //StartCoroutine("EnableAutoScroll");
                }
            }
            var d = SwipeDirection.Left;
            if (_startPos.x < Input.mousePosition.x)
                d = SwipeDirection.Right;
            var distance = 0.1f;
            distance = Vector2.Distance(Input.mousePosition, _startPos) * _swipeSpeed * Time.deltaTime / 2f;
            if(enableScrolling) ScrollAndSnap(distance, d);
            _startPos = Input.mousePosition;
        }

        /// <summary>
        /// Checking for touch input by users
        /// </summary>
        private void CheckTouchInput()
        {
            if (!_isScrollableDefault) return;
            if (Input.touches.Length <= 0) return;
            if(!_isMouseScrolling) return;
            var touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    _initPos = touch.position;
                    _startTime = Time.time;
                    StopCoroutine("SnapAnimation");
                    StopCoroutine("ScrollContent");
                    break;
                case TouchPhase.Moved:
                {
                    if (isAutoScroll)
                    {
                        _autoScrollEnableTime = 5f;
                        if (_isAutoScrollRunning)
                        {
                            StopAutoScroll();
                            //StartCoroutine("EnableAutoScroll");
                        }
                    }
                    _startPos = touch.deltaPosition;
                    _endPos = touch.position;
                    _endTime = Time.time;
                    var distance = 0.1f;
                    distance = Vector2.Distance(_startPos, _endPos) * _swipeSpeed * Time.deltaTime / 2f;
                    //Debug.Log("Distance : " + distance);
                    var d = SwipeDirection.Left;
                    if (_startPos.x < _endPos.x) d = SwipeDirection.Right;
                    if(enableScrolling) ScrollAndSnap(distance, d);
                    break;
                }
                case TouchPhase.Ended:
                    _endPos = touch.position;
                    _endTime = Time.time;
                    if (enableScrolling)
                    {
                        if (CheckSwipe() == false) StartCoroutine("SnapAnimation", CalculateDistance());
                    }
                    else
                    {
                        if(CheckSwipe()) ScrollToNext();
                    }
                    break;
                case TouchPhase.Stationary:
                    break;
                case TouchPhase.Canceled:
                    _endPos = touch.position;
                    _endTime = Time.time;
                    if (CheckSwipe() == false) StartCoroutine("SnapAnimation", CalculateDistance());
                    break;
                default:
                    Debug.Log("Unknown State");
                    break;
            }
        }

        /// <summary>
        /// Detect swipe gesture or not from predefined swipe threshold. Scrolling behaviour will only work if enableScrolling is true
        /// </summary>
        /// <returns>Returns true if swipe gesture is detected</returns>
        private bool CheckSwipe()
        {
            var distance = Vector2.Distance(_initPos, _endPos);
            var timeTaken = _endTime - _startTime;
            var d = _endPos.x < _initPos.x ? SwipeDirection.Left : SwipeDirection.Right;
            if (!(distance >= distanceThreshHold) || !(timeTaken <= timeThreshHold)) return false;
            var data = new SwipeData
            {
                Velocity = new Vector2(distance * timeTaken * scrollSensitivity, 0), Direction = d
            };
            if (!enableScrolling) return false;
            StartCoroutine("ScrollContent", data);
            return true;
        }

        /// <summary>
        /// Scroll and snap to nearest item in a scroll view.
        /// </summary>
        /// <param name="distance">Distance that needs to move all object</param>
        /// <param name="d">Direction to move towards</param>
        private void ScrollAndSnap(float distance, SwipeDirection d)
        {
            var closePos = float.MaxValue;
            if (d == SwipeDirection.Left)
            {
                for (var i = 0; i < _contentContainer.childCount; i++)
                {
                    var t = _contentContainer.GetChild(i);
                    Vector2 pos = t.localPosition;
                    pos.x -= distance;
                    if (pos.x <= -_itemWidth)
                    {
                        pos.x = _maxXPos - _itemWidth - (Mathf.Abs(pos.x) - _itemWidth);
                    }

                    t.localPosition = pos;
                    var x = Mathf.Abs(pos.x);
                    if (x > closePos) continue;
                    _closeOne = t;
                    _currentIndex = i;
                    closePos = x;
                }
            }
            else
            {
                for (var i = 0; i < _contentContainer.childCount; i++)
                {
                    var t = _contentContainer.GetChild(i);
                    Vector2 pos = t.localPosition;
                    pos.x += distance;
                    if (pos.x >= _maxXPos - _itemWidth)
                    {
                        pos.x = -_itemWidth + pos.x - (_maxXPos - _itemWidth);
                    }

                    t.localPosition = pos;
                    var x = Mathf.Abs(pos.x);
                    if (x > closePos) continue;
                    _closeOne = t;
                    _currentIndex = i;
                    closePos = x;
                }
            }
        }

        /// <summary>
        /// Start auto scroll
        /// </summary>
        private void StartAutoScroll()
        {
            if(!_isScrollableDefault) return;
            InvokeRepeating("ScrollToNext", autoScrollDelay, autoScrollDelay);
            _isAutoScrollRunning = true;
        }

        /// <summary>
        /// stops auto scroll
        /// </summary>
        private void StopAutoScroll()
        {
            if (_isAutoScrollRunning)
            {
                _isAutoScrollRunning = false;
                _isAutoScrollStopTemp = true;
                CancelInvoke("ScrollToNext");
            }
        }
        private SnapData CalculateDistance()
        {
            var x = _closeOne.localPosition.x;
            var isAdd = false;
            if (x < 0)
            {
                x = Mathf.Abs(x);
                isAdd = true;
            }

            var data = new SnapData {Distance = x, Direction = isAdd};
            return data;
        }

        #endregion

        #region PUBLIC_METHODS

        public void RemoveAllChild()
        {
            if(_isScrollable) return;
            StopAllCoroutines();
            if (_contentContainer == null)
            {
                _contentContainer = transform.GetChild(0).transform as RectTransform;
            }
            if(_contentContainer.childCount == 0) return;
            foreach (Transform trans in _contentContainer)
            {
                Destroy(trans.gameObject);
            }
        }
        
        public void AddChildren(List<GameObject> items)
        {
            if(_isScrollable) return;
            if(items.Count == 0) return;
            StopAllCoroutines();
            if (_contentContainer == null)
            {
                _contentContainer = transform.GetChild(0) as RectTransform;
            }
            StartCoroutine("AddChildrenNextFrame", items);
        }

        public void AddItem(GameObject g)
        {
            if(_isScrollable) return;
            if (_contentContainer == null)
            {
                _contentContainer = transform.GetChild(0).transform as RectTransform;
            }
            var item = ((RectTransform) g.transform);
            item.SetParent(_contentContainer);
            item.anchorMin = new Vector2(0, 1);
            item.anchorMax = new Vector2(0, 1);
            item.sizeDelta = new Vector2(_itemWidth, _contentContainer.sizeDelta.y);
            item.pivot = new Vector2(0, 1);
            item.localPosition = new Vector3(_itemWidth * _contentContainer.childCount, 0);
            item.localScale = Vector3.one;
        }

        public void OnPointerDown(BaseEventData data)
        {
            Debug.Log("OnPointerDown");
            _initPos = _startPos = Input.mousePosition;
            _startTime = Time.time;
            _isMouseScrolling = true;
            StopCoroutine("SnapAnimation");
            StopCoroutine("ScrollContent");
        }

        public void OnPointerUp(BaseEventData data)
        {
            Debug.Log("OnPointerUp");
            _isMouseScrolling = false;
            _endPos = Input.mousePosition;
            _endTime = Time.time;
            if (enableScrolling)
            {
                if (CheckSwipe() == false) StartCoroutine("SnapAnimation", CalculateDistance());
            }
            else
            {
                if(CheckSwipe()) ScrollToNext();
            }

            /*var x = _closeOne.localPosition.x;
            var isAdd = false;
            if (x < 0)
            {
                x = Mathf.Abs(x);
                isAdd = true;
            }
            
            for (int i = 0; i < _contentContainer.childCount; i++)
            {
                var item = _contentContainer.GetChild(i);
                Vector2 pos = item.localPosition;
                pos = isAdd ? new Vector3(pos.x + x, pos.y) : new Vector3(pos.x - x, pos.y);
    
                if (item.localPosition.x <= -_itemWidth)
                {
                    pos = new Vector3(_maxXPos - _itemWidth,
                        pos.y);
                }
                else if (item.localPosition.x > _maxXPos - _itemWidth)
                {
                    pos = new Vector3(-_itemWidth, pos.y);
                }
    
                item.localPosition = pos;
            }*/
        }

        public void ScrollToNext()
        {
            if (_currentIndex + 1 < _contentContainer.childCount)
                _currentIndex += 1;
            else
                _currentIndex = 0;

            StartCoroutine("SnapAnimation", new SnapData {Distance = _itemWidth, Direction = false});
        }
        
        #endregion

        #region CO-ROUTINES

        private IEnumerator ScrollContent(SwipeData data)
        {
            if (!gameObject.activeSelf) yield break; 
            var distance = float.MaxValue;
            while (distance > snapLimit)
            {
                yield return 0;
                distance = scrollSpeed * data.Velocity.x * Time.deltaTime;
                data.Velocity.x -= distance * Time.deltaTime * stopSpeed;
                ScrollAndSnap(distance, data.Direction);
            }

            StartCoroutine("SnapAnimation", CalculateDistance());
        }

        private IEnumerator SnapAnimation(SnapData data)
        {
            if(!gameObject.activeSelf) yield break;
            float t = 0;
            while (t < animationTime)
            {
                yield return 0;
                var factor = Time.deltaTime * data.Distance / animationTime;
                t += Time.deltaTime;
                for (var i = 0; i < _contentContainer.childCount; i++)
                {
                    var item = _contentContainer.GetChild(i);
                    Vector2 pos = item.localPosition;
                    pos = data.Direction ? new Vector3(pos.x + factor, pos.y) : new Vector3(pos.x - factor, pos.y);

                    if (item.localPosition.x <= -_itemWidth)
                    {
                        //pos.x = _maxXPos - _itemWidth - (Mathf.Abs(pos.x) - _itemWidth);
                        pos = new Vector3(_maxXPos - _itemWidth - (Mathf.Abs(pos.x) - _itemWidth), pos.y);
                    }
                    else if (item.localPosition.x >= _maxXPos - _itemWidth)
                    {
                        // pos.x = -_itemWidth + pos.x - (_maxXPos - _itemWidth);
                        pos = new Vector3(-_itemWidth + pos.x - (_maxXPos - _itemWidth), pos.y);
                    }

                    item.localPosition = pos;
                }
            }

            _contentContainer.GetChild(_currentIndex).localPosition = new Vector3(0, 0);
            if (_currentIndex + 1 < _contentContainer.childCount)
            {
                _contentContainer.GetChild(_currentIndex + 1).localPosition = new Vector3(_itemWidth, 0);
            }

            if (_currentIndex - 1 != -1)
            {
                _contentContainer.GetChild(_currentIndex - 1).localPosition = new Vector3(-_itemWidth, 0);
            }

            for (var i = _currentIndex + 2; i < _contentContainer.childCount - 2; i++)
            {
                _contentContainer.GetChild(i).localPosition = new Vector3(_itemWidth * (i - 1), 0);
            }
        }

        private IEnumerator AddChildrenNextFrame(List<GameObject> items)
        {
            yield return 0;
            foreach (var g in items)
            {
                g.transform.SetParent(_contentContainer);
            }
            _isScrollableDefault = true;
            OnUpdateLayout();
        }

        #endregion

        #region EDITOR_METHODS
        public void OnUpdateLayout()
        {
            //Debug.Log("Called Now : ");
            _currentIndex = 0;
            if (transform.childCount == 0)
            {
                var c = new GameObject("Content", typeof(RectTransform));
                c.transform.SetParent(transform);
                c.transform.localScale = new Vector3(1,1,1);
                c.transform.localRotation = Quaternion.identity;
                _contentContainer = c.transform as RectTransform;
            }
            if (_contentContainer == null)
            {
                _contentContainer =  transform.GetChild(0) as RectTransform;
            }
            
            //Debug.Log("My Rect : " + ((RectTransform) transform).rect.ToString());

            if (ReferenceEquals(_contentContainer, null)) return;
            _contentContainer.localPosition = Vector3.zero;
            _contentContainer.anchorMin = new Vector2(0, 1);
            _contentContainer.anchorMax = new Vector2(0, 1);
            _contentContainer.pivot = new Vector2(0, 1);
            _contentContainer.anchoredPosition = new Vector2(0,0);
            var current = ((RectTransform) transform).rect;
            _contentContainer.sizeDelta = new Vector2(current.width, current.height);

            _itemWidth = _contentContainer.sizeDelta.x;
            _maxXPos = _itemWidth * _contentContainer.childCount;

            _swipeSpeed =  _itemWidth / Screen.width;
            //Debug.Log("Swipe Speed : " + _swipeSpeed);

            if (_contentContainer.childCount <= 0) return;
            for (var i = 0; i < _contentContainer.childCount; i++)
            {
                var item = ((RectTransform) _contentContainer.GetChild(i).transform);
                item.anchorMin = new Vector2(0, 1);
                item.anchorMax = new Vector2(0, 1);
                item.sizeDelta = new Vector2(_itemWidth, _contentContainer.sizeDelta.y);
                item.pivot = new Vector2(0, 1);
                item.localPosition = new Vector3(_itemWidth * i, 0);
                item.localScale = Vector3.one;
            }
            _closeOne = _contentContainer.GetChild(0);
        }
        #endregion

        #region OTHERS

        private enum SwipeDirection
        {
            Left,
            Right
        }

        private struct SwipeData
        {
            public Vector2 Velocity;
            public SwipeDirection Direction;
        }

        private struct SnapData
        {
            public float Distance;
            public bool Direction;
        }

        #endregion
    }
}