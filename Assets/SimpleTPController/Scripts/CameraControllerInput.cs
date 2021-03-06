﻿using UnityEngine;

namespace ThirdPersonController
{
    [RequireComponent(typeof(CharacterMotor))]
    public class CameraControllerInput : MonoBehaviour, ICameraStateController, ICameraControllerInput
    {
        [SerializeField] private GameObject m_CameraControllerPrefab = null;
        [SerializeField] private CameraControllerInputSettings m_DefaultSettings = null;

        private CharacterMotor m_CharacterMotor;

        private float m_XInput;
        private float m_YInput;

        /// <summary>
        /// The current camera controller.
        /// </summary>
        public ICameraController cameraController { get; private set; }

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// </summary>
        protected virtual void Awake()
        {
            m_CharacterMotor = GetComponent<CharacterMotor>();
        }

        /// <summary>
        /// Start is called on the frame when a script is enabled just before
        /// any of the Update methods is called the first time.
        /// </summary>
        protected virtual void Start()
        {
            cameraController = Instantiate(m_CameraControllerPrefab).GetComponent<ICameraController>();
            cameraController.SetTarget(transform);
        }

        /// <summary>
        /// Update is called every frame, if the MonoBehaviour is enabled.
        /// </summary>
        protected virtual void Update()
        {
            m_XInput = InputManager.GetAxis(m_DefaultSettings.mouseXInput);
            m_YInput = InputManager.GetAxis(m_DefaultSettings.mouseYInput);
        }

        /// <summary>
        /// This function is called every fixed framerate frame, if the MonoBehaviour is enabled.
        /// </summary>
        protected virtual void FixedUpdate()
        {
            cameraController.Rotate(m_XInput, m_YInput);
            m_CharacterMotor.Rotate(cameraController.yRotation);
        }

        public virtual string GetCurrentState()
        {
            return
                m_CharacterMotor.isCrouching ? m_DefaultSettings.crouchingStateName :
                m_CharacterMotor.isSprinting ? m_DefaultSettings.sprintingStateName :
                m_DefaultSettings.defaultStateName;
        }
    }
}