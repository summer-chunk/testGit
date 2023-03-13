using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CameraMove : MonoBehaviour
{
    public GameObject firstPerson;
    public GameObject[] UAV;
    public float Speed = 1f;
    public float UpSpeed = 1f;
    System.DateTime preTime;
    Vector3 direction = Vector3.zero;
    public Transform[] UAVpath;
    public Transform[] UAVpath1;
    public Transform[] UAVpath2;
    public Transform[] UAVpath3;
    public Transform[] UAVpath4;
    int currentUAVIndex;
    int currentUAVIndex1;
    int currentUAVIndex2;
    int currentUAVIndex3;
    int currentUAVIndex4;
    [HideInInspector]
    public Transform[] cameraPositions;
    int screenShotCount = 0;
    int groupPersonCount = 0;
    int[] currentFramePersonCount;
    int groupTimeSpan;
    int groupIndex = 0;
    [HideInInspector]
    public int pathIndex = 0;
    int maxCameraCount = 5;
    public PersonCountEnumUAV personCountChosen;

    float personHeight = 0.43f;
    float personWidth = 0.15f;
    float personThick = 0.064f;
    float heightOffset = 0.02f;

    List<int> currentFbxIndices;
    List<string> groupIndicies;
    List<int> personCountList;
    Dictionary<int, int[]> fbxUsingRange;
    Dictionary<int, int> startGroupIds;
    int[][] limitedPersonCount;
    GameObject[] scenePersons = new GameObject[0];
    public GameObject[] startPositionParent;
    public GameObject[] endPositionParent;
    float nextShotTime = 1f;
    static int tolerance = 0;
    static int GroupCount = 0;
    GameObject fbxModelPrefab;
    public RuntimeAnimatorController walkController;
    Camera mainCamera;
    public Camera[] cameras;
    RenderTexture captureRT;
    public bool isWalkCircle = false;
    Vector2 screenPosition;
    Vector3[] cubeEdgePositions;
    Vector3[] cubeEdgeScreenPositions;
    int[] startPositionIndices;
    int[] endPositionIndices;
    const string datasetImagesPath = "D:/toyDataset_train";
    string trainTestStatus;

    public static float runtimeRatio = 3f;

    int screenShotIndex;
    float shotTimeInterval;
    float accumulateTime;
    string currentTextLine = "";
    string currentWriteTxt = "";
    List<Rect> currentBoundings;
    List<float> overlapAreas;

    bool currentIsNight = false;

    public LightingSettings nightLightingSetting;
    LightingSettings dayLightingSetting;

    SineCurveUAV lightHCurve;
    SineCurveUAV lightSCurve;
    SineCurveUAV lightVCurve;
    public GameObject dayLight;
    public GameObject nightLight;
    public GameObject roadLights;
    public Material defaultSkyboxMaterial;

    [HideInInspector]
    public bool gameIsPaused;

    List<System.Tuple<string, byte[]>> saveJpgBuffer;


    public float circleRadius;
    int cameraID;

    void Awake()
    {
        startPositionIndices = new int[6];
        endPositionIndices = new int[6];
        for (int i = 0; i < 6; i++)
        {
            startPositionIndices[i] = i;
            endPositionIndices[i] = 5 - i;
        }

        ShuffleStartEndPositions();

        isWalkCircle = true;
        circleRadius = 0.8f; // 0.6f;
        groupTimeSpan = Random.Range(14, 16);
        runtimeRatio = 5f;
        shotTimeInterval = .05f / runtimeRatio;
        // shotTimeInterval = 0.005f;
        preTime = System.DateTime.Now;
        currentUAVIndex = 0;
        currentUAVIndex1 = 0;
        currentUAVIndex2 = 0;
        currentUAVIndex3 = 0;
        currentUAVIndex4 = 0;
        cameraID = 0;
        trainTestStatus = "train";
        //groupIndicies = File.ReadAllLines("Assets/Resources/person_models/groupSplits/" + string.Format("{0}Group_part2", trainTestStatus) + ".txt").ToList();
        groupIndicies = File.ReadAllLines("Assets/Resources/person_models/groupSplits/toyDatatset_train.txt").ToList();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (!Directory.Exists(datasetImagesPath + "/Cam"))
        {
            for (int i = 1; i <= maxCameraCount; i++)
            {
                Directory.CreateDirectory(datasetImagesPath + "/Cam_" + i.ToString());
            }
        }

        saveJpgBuffer = new List<System.Tuple<string, byte[]>>();
        Thread saveJpgReader = new Thread(SaveJpgThreadFunction);
        saveJpgReader.Start();

        SetPersonCountList();

        //mainCamera = GetComponent<Camera>();
        captureRT = new RenderTexture(Screen.width, Screen.height, 0);
        cubeEdgePositions = new Vector3[12];
        cubeEdgeScreenPositions = new Vector3[12];
        currentFramePersonCount = new int[5]; // Initiation
        currentBoundings = new List<Rect>();
        overlapAreas = new List<float>();

        currentFbxIndices = new List<int>();

        SelectedPersons();
        lightHCurve = new SineCurveUAV(45, 45, runtimeRatio);
        lightSCurve = new SineCurveUAV(15, 15, runtimeRatio);
        lightVCurve = new SineCurveUAV(15, 70, runtimeRatio);
    }

    // Update is called once per frame
    void Update()
    {
        HandleGlobalLight();
        // HandleUAVActions();
        // HandleCameraShot(0);
        // HandleUAVActions1();
        // HandleCameraShot(1);
        // HandleUAVActions2();
        // HandleCameraShot(2);
        // HandleUAVActions3();
        // HandleCameraShot(3);
        // HandleUAVActions4();
        // HandleCameraShot(4);
        if (cameraID == 0) HandleUAVActions();
        else if (cameraID == 1) HandleUAVActions1();
        else if (cameraID == 2) HandleUAVActions2();
        else if (cameraID == 3) HandleUAVActions3();
        else HandleUAVActions4();
        HandleCameraShot();
    }

    void ShuffleStartEndPositions()
    {

        if (isWalkCircle)
        {
            startPositionIndices = new int[6] { 0, 1, 2, 3, 4, 5 };
            endPositionIndices = new int[6] { 0, 1, 2, 3, 4, 5 };
            return;
        }

        byte[] keys = new byte[startPositionIndices.Length];
        new System.Random().NextBytes(keys);
        System.Array.Sort(keys, startPositionIndices);
        new System.Random().NextBytes(keys);
        System.Array.Sort(keys, endPositionIndices);

        for (int i = 0; i < startPositionIndices.Length; i++)
        {
            if (startPositionIndices[i] == 0)
            {
                Swap(ref startPositionIndices[i], ref startPositionIndices[0]);
                break;
            }
        }
        for (int i = 0; i < endPositionIndices.Length; i++)
        {
            if (endPositionIndices[i] == 0)
            {
                Swap(ref endPositionIndices[i], ref endPositionIndices[0]);
                break;
            }
        }
    }

    void HandleCameraShot()
    {
        if (Time.time > nextShotTime)
        {
            if (gameIsPaused)
            {
                nextShotTime = Time.time + shotTimeInterval;
                return;
            }

            currentFramePersonCount[cameraID] = 0;
            currentTextLine = "";
            currentBoundings.Clear();
            overlapAreas.Clear();

            bool groupValid = true;
            for (int i = 0; i < groupPersonCount; i++)
            {
                if (!Get12EdgePositions(i))
                {
                    groupValid = false;
                    break;
                }
            }
            if (groupValid)
            {
                CaptureCameraShot();

            }
            HandleSceneSwitch();
        }
    }

    bool Get12EdgePositions(int personi)
    {
        firstPerson = scenePersons[personi];

        Vector3 personBottomPosition = firstPerson.transform.position;
        cubeEdgePositions[0] = personBottomPosition + firstPerson.transform.forward * personThick / 2;
        cubeEdgePositions[1] = personBottomPosition + firstPerson.transform.right * personWidth / 2;
        cubeEdgePositions[2] = personBottomPosition - firstPerson.transform.forward * personThick / 2;
        cubeEdgePositions[3] = personBottomPosition - firstPerson.transform.right * personWidth / 2;

        Vector3 personCenterPosition = personBottomPosition + firstPerson.transform.up * personHeight / 2;

        cubeEdgePositions[4] = personCenterPosition + firstPerson.transform.forward * personThick / 2 + firstPerson.transform.right * personWidth / 2;
        cubeEdgePositions[5] = personCenterPosition - firstPerson.transform.forward * personThick / 2 + firstPerson.transform.right * personWidth / 2;
        cubeEdgePositions[6] = personCenterPosition - firstPerson.transform.forward * personThick / 2 - firstPerson.transform.right * personWidth / 2;
        cubeEdgePositions[7] = personCenterPosition + firstPerson.transform.forward * personThick / 2 - firstPerson.transform.right * personWidth / 2;

        cubeEdgePositions[8] = cubeEdgePositions[0] + firstPerson.transform.up * personHeight;
        cubeEdgePositions[9] = cubeEdgePositions[1] + firstPerson.transform.up * personHeight;
        cubeEdgePositions[10] = cubeEdgePositions[2] + firstPerson.transform.up * personHeight;
        cubeEdgePositions[11] = cubeEdgePositions[3] + firstPerson.transform.up * personHeight;

        for (int i = 0; i < cubeEdgePositions.Length; i++)
        {
            cubeEdgePositions[i].y -= heightOffset;
        }

        for (int i = 0; i < 12; i++)
        {
            cubeEdgeScreenPositions[i] = cameras[cameraID].WorldToScreenPoint(cubeEdgePositions[i]);
            if (cubeEdgeScreenPositions[i].x < float.Epsilon || cubeEdgeScreenPositions[i].y < float.Epsilon
                || cubeEdgeScreenPositions[i].x > Screen.width || cubeEdgeScreenPositions[i].y > Screen.height)
            {
                return true;
            }
            cubeEdgeScreenPositions[i].y = Screen.height - cubeEdgeScreenPositions[i].y;
        }

        float top = cubeEdgeScreenPositions[0].y, bottom = cubeEdgeScreenPositions[0].y, left = cubeEdgeScreenPositions[0].x, right = cubeEdgeScreenPositions[0].x;

        for (int i = 1; i < 12; i++)
        {
            top = Mathf.Min(top, cubeEdgeScreenPositions[i].y);
            bottom = Mathf.Max(bottom, cubeEdgeScreenPositions[i].y);
            left = Mathf.Min(left, cubeEdgeScreenPositions[i].x);
            right = Mathf.Max(right, cubeEdgeScreenPositions[i].x);
        }

        //GUI.Box(new Rect(new Vector2(left, bottom), new Vector2(right - left, top - bottom)), "");
        if (!IsValidBoundings(new Rect(left, top, right - left, bottom - top)))
        {
            return false;
        }

        currentFramePersonCount[cameraID]++;
        currentTextLine += (currentFbxIndices[personi].ToString() + "," + Round4String(left) + "," + Round4String(top) +
        "," + Round4String(right - left) + "," + Round4String(bottom - top) + "," +
        Round4String(Vector3.Distance(cameras[cameraID].transform.position, scenePersons[personi].transform.position)) + " ");

        return true;
    }

    void CaptureCameraShot()
    {
        if (limitedPersonCount[cameraID][currentFramePersonCount[cameraID]] <= 0)
        {
            return;
        }
        else
        {
            limitedPersonCount[cameraID][currentFramePersonCount[cameraID]]--;
        }
        nextShotTime = Time.time + shotTimeInterval;
        cameras[cameraID].targetTexture = captureRT;
        RenderTexture.active = captureRT;

        cameras[cameraID].Render();

        Texture2D screenShot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        screenShot.ReadPixels(Rect.MinMaxRect(0, 0, Screen.width, Screen.height), 0, 0);
        screenShot.Apply();

        cameras[cameraID].targetTexture = null;
        RenderTexture.active = null;

        byte[] jpgBytes = screenShot.EncodeToJPG();

        string dayNightStr;
        if (currentIsNight)
        {
            dayNightStr = "night";
        }
        else
        {
            dayNightStr = "day";
        }

        long time = (long)(System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds);
        string jpgName = "/Cam_" + (cameraID + 1).ToString() + "/" + "cam" + (cameraID + 1).ToString() + "_" + trainTestStatus + "_" + dayNightStr +
        "_" + IntAddZeros(++screenShotIndex).ToString() + "_" + (groupIndex - 1).ToString() + "_" + time.ToString() + ".jpg";
        currentTextLine = jpgName + " " + (groupIndex - 1).ToString() + " " + currentTextLine;
        using (StreamWriter temp = new StreamWriter(datasetImagesPath + "/pCount_Unvalid.txt", true))
        {
            temp.WriteLine(jpgName);
        }
        saveJpgBuffer.Add(System.Tuple.Create(jpgName, (byte[])jpgBytes.Clone()));
        print("Save Picture: " + jpgName);
        screenShotCount++;
        currentWriteTxt += currentTextLine + '\n';
    }

    void HandleSceneSwitch()
    {
        bool flag1 = limitedPersonCount[cameraID].Sum() == 0;
        bool flag2 = (tolerance > 300) && (limitedPersonCount[cameraID].Sum() <= 2); // at least capture one
        if (flag1 || flag2)
        {
            //print("Going into HandleSceneSwitch, and current camera id = " + cameraID.ToString());
            if (flag2)
            {
                print("Final tolerance trigger!!!! Current camera id = " + cameraID.ToString());
            }
            pathIndex++;
            if (pathIndex >= 3 && pathIndex <= 6) cameraID++;
            else if (pathIndex >= 7) cameraID = 0;

            tolerance = 0;


            if (pathIndex >= 10)
            {
                pathIndex = 0;

                if (currentIsNight)
                {
                    SetNight2Day();
                    currentIsNight = false;
                }
                else
                {
                    SetDay2Night();
                    currentIsNight = true;
                }
                cameraID = 0;
            }

            SaveLabelTxt();

            if (--groupTimeSpan <= 0)
            {
                groupTimeSpan = Random.Range(14, 16);
                for (int i = 0; i < groupPersonCount; i++)
                {
                    Destroy(scenePersons[i]);
                }
                Resources.UnloadUnusedAssets();
                SelectedPersons();
            }

            ShuffleStartEndPositions();
            screenShotCount = 0;

            for (int i = 0; i < scenePersons.Length; i++)
            {
                PersonMoveUAV curScript = scenePersons[i].GetComponent<PersonMoveUAV>();
                if (isWalkCircle)
                {
                    if (i == 0)
                    {
                        curScript.weightPoints[0] = startPositionParent[pathIndex].transform.GetChild(startPositionIndices[0]);
                        curScript.weightPoints[1] = endPositionParent[pathIndex].transform.GetChild(endPositionIndices[0]);
                        curScript.ResetPosition();
                    }
                    else
                    {
                        curScript.weightPoints[0] = startPositionParent[pathIndex].transform.GetChild(startPositionIndices[1]);
                        curScript.weightPoints[1] = endPositionParent[pathIndex].transform.GetChild(endPositionIndices[1]);
                        curScript.ResetPosition(startPositionIndices[i % 6]);
                    }
                }
                else
                {
                    curScript.weightPoints[0] = startPositionParent[pathIndex].transform.GetChild(startPositionIndices[i]);
                    curScript.weightPoints[1] = endPositionParent[pathIndex].transform.GetChild(endPositionIndices[i]);
                    curScript.ResetPosition();
                }

            }
        }
        else
        {
            tolerance++;
        }
    }

    void SelectedPersons()
    {
        System.DateTime now = System.DateTime.Now;
        System.TimeSpan ts = now.Subtract(preTime);
        preTime = System.DateTime.Now;
        string t = ts.TotalSeconds.ToString();
        string costtime = groupIndex.ToString() + ' ' + t + 's' + '\n';
        Thread txtThread1 = new Thread((txtPath) => {
            string[] strs = (string[])txtPath;
            using (StreamWriter temp = new StreamWriter(strs[0], true))
            {
                temp.Write(strs[1]);
            }
        });
        txtThread1.Start(new string[2] {
            datasetImagesPath + "/costTime.txt", costtime
        });
        List<string> curGroupFbx = new List<string>();
        currentFbxIndices.Clear();
        if (groupIndex >= groupIndicies.Count) QuitGame();
        var words = groupIndicies[groupIndex].Split(',');
        foreach (var word in words)
        {
            curGroupFbx.Add(word);
        }
        groupPersonCount = curGroupFbx.Count();
        SetLimitedPersonCount();

        scenePersons = new GameObject[groupPersonCount];
        groupIndex++;

        for (int i = 0; i < groupPersonCount; i++)
        {
            string currentFbxIndex = curGroupFbx[i];
            int FbxIndexInt = int.Parse(currentFbxIndex);
            currentFbxIndices.Add(FbxIndexInt);
            if (FbxIndexInt > 31000)
            {
                Debug.LogError("Person FBX index Overflow!");
                QuitGame();
            }
            string modelNameExt; // person prefab path
            if (currentFbxIndex.Length < 2) modelNameExt = "000" + currentFbxIndex.ToString();
            else if (currentFbxIndex.Length < 3) modelNameExt = "00" + currentFbxIndex.ToString();
            else if (currentFbxIndex.Length < 4) modelNameExt = "0" + currentFbxIndex.ToString();
            else modelNameExt = currentFbxIndex.ToString();

            modelNameExt = "person_models/exports_" + trainTestStatus + string.Format("/City1Mv2_{0}_{1}", trainTestStatus, modelNameExt);
            fbxModelPrefab = (GameObject)Resources.Load(modelNameExt); // load model
            if (fbxModelPrefab == null)
            {
                Debug.LogError("No fbx model!");
                continue;
            }

            GameObject personTest = Instantiate(fbxModelPrefab, Vector3.zero, Quaternion.identity);

            Animator currentAnimator = personTest.AddComponent<Animator>();
            currentAnimator.runtimeAnimatorController = walkController;
            currentAnimator.speed = (UnityEngine.Random.value * 0.6f + 0.7f) * runtimeRatio;
            currentAnimator.applyRootMotion = true;
            Rigidbody curRigidbody = personTest.AddComponent<Rigidbody>();
            curRigidbody.useGravity = false;
            if (i == 0) curRigidbody.mass = 100f;
            curRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            curRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

            personTest.transform.localScale = Vector3.one * 0.025f;
            var currentPersonMoveUAV = personTest.AddComponent<PersonMoveUAV>();
            currentPersonMoveUAV.moveSpeed = (UnityEngine.Random.value * 0.3f + 0.5f) * runtimeRatio;

            scenePersons[i] = personTest;
            if (isWalkCircle)
            {
                if (i == 0)
                {
                    currentPersonMoveUAV.isCenterPerson = true;
                    currentPersonMoveUAV.moveSpeed = (UnityEngine.Random.value * 0.3f + 0.5f) * runtimeRatio;
                    currentPersonMoveUAV.weightPoints[0] = startPositionParent[pathIndex].transform.GetChild(startPositionIndices[i]);
                    currentPersonMoveUAV.weightPoints[1] = endPositionParent[pathIndex].transform.GetChild(endPositionIndices[i]);
                    currentPersonMoveUAV.ResetPosition();
                    // print("i = " + i);
                    // print("currentPersonMoveUAV.transform.position = " + currentPersonMoveUAV.transform.position);

                }
                else
                {
                    currentPersonMoveUAV.centerPerson = scenePersons[0];
                    currentPersonMoveUAV.ResetPosition(startPositionIndices[i]);
                    // print("i = " + i);
                    // print("currentPersonMoveUAV.transform.position = " + currentPersonMoveUAV.transform.position);

                }
            }
            else
            {
                currentPersonMoveUAV.isCenterPerson = true;
                currentPersonMoveUAV.weightPoints[0] = startPositionParent[pathIndex].transform.GetChild(startPositionIndices[i]);
                currentPersonMoveUAV.weightPoints[1] = endPositionParent[pathIndex].transform.GetChild(endPositionIndices[i]);
                currentPersonMoveUAV.ResetPosition();
            }
            for (int j = 0; j < personTest.transform.childCount; j++)
            {
                GameObject partObject = personTest.transform.GetChild(j).gameObject;
                if (partObject.GetComponent<SkinnedMeshRenderer>() != null)
                {
                    MeshCollider curMesh = partObject.AddComponent<MeshCollider>();
                    curMesh.convex = true;
                    curMesh.sharedMesh = partObject.GetComponent<SkinnedMeshRenderer>().sharedMesh;
                }
            }
        }
        scenePersons[0].GetComponent<PersonMoveUAV>().isCenterPerson = true;
    }

    void SaveLabelTxt()
    {
        if (currentWriteTxt != "")
        {
            Thread txtThread1 = new Thread((txtPath) => {
                string[] strs = (string[])txtPath;
                using (StreamWriter temp = new StreamWriter(strs[0], true))
                {
                    temp.Write(strs[1]);
                }
            });
            txtThread1.Start(new string[2] {
                datasetImagesPath + "/train1_pCount" + groupPersonCount.ToString() + ".txt", (string)currentWriteTxt.Clone()
            });

            Thread txtThread2 = new Thread((txtPath) => {
                string[] strs = (string[])txtPath;
                using (StreamWriter temp = new StreamWriter(strs[0], true))
                {
                    temp.Write(strs[1]);
                }
            });
            txtThread2.Start(new string[2] {
                datasetImagesPath + "/train1_allCount.txt", (string)currentWriteTxt.Clone()
            });
            currentWriteTxt = "";
        }
        // Debug.Log("Current Count of Jpg(s) To SAVE: " + saveJpgBuffer.Count.ToString());
    }

    void HandleUAVActions()
    {
        if (gameIsPaused) return;
        int nextIndex = (currentUAVIndex + 1) % UAVpath.Length;
        Vector3 targetPosition = UAVpath[nextIndex].position;
        Vector3 targetDirection = (targetPosition - UAV[0].transform.position).normalized;
        // targetDirection.y = 0f;
        UAV[0].transform.Translate(targetDirection * Speed * Time.deltaTime, Space.World);

        if (Vector3.Distance(UAV[0].transform.position, targetPosition) < 0.1f)
        {
            currentUAVIndex = nextIndex;
        }
    }

    void HandleUAVActions1()
    {
        if (gameIsPaused) return;
        int nextIndex = (currentUAVIndex1 + 1) % UAVpath1.Length;
        Vector3 targetPosition = UAVpath1[nextIndex].position;
        Vector3 targetDirection = (targetPosition - UAV[1].transform.position).normalized;
        // targetDirection.y = 0f;
        UAV[1].transform.Translate(targetDirection * Speed * Time.deltaTime, Space.World);

        if (Vector3.Distance(UAV[1].transform.position, targetPosition) < 0.1f)
        {
            currentUAVIndex1 = nextIndex;
        }
    }

    void HandleUAVActions2()
    {
        if (gameIsPaused) return;
        int nextIndex = (currentUAVIndex2 + 1) % UAVpath2.Length;
        Vector3 targetPosition = UAVpath2[nextIndex].position;
        Vector3 targetDirection = (targetPosition - UAV[2].transform.position).normalized;
        // targetDirection.y = 0f;
        UAV[2].transform.Translate(targetDirection * Speed * Time.deltaTime, Space.World);

        if (Vector3.Distance(UAV[2].transform.position, targetPosition) < 0.1f)
        {
            currentUAVIndex2 = nextIndex;
        }
    }

    void HandleUAVActions3()
    {
        if (gameIsPaused) return;
        int nextIndex = (currentUAVIndex3 + 1) % UAVpath3.Length;
        Vector3 targetPosition = UAVpath3[nextIndex].position;
        Vector3 targetDirection = (targetPosition - UAV[3].transform.position).normalized;
        // targetDirection.y = 0f;
        UAV[3].transform.Translate(targetDirection * Speed * Time.deltaTime, Space.World);

        if (Vector3.Distance(UAV[3].transform.position, targetPosition) < 0.1f)
        {
            currentUAVIndex3 = nextIndex;
        }
    }

    void HandleUAVActions4()
    {
        if (gameIsPaused) return;
        int nextIndex = (currentUAVIndex4 + 1) % UAVpath4.Length;
        Vector3 targetPosition = UAVpath4[nextIndex].position;
        Vector3 targetDirection = (targetPosition - UAV[4].transform.position).normalized;
        // targetDirection.y = 0f;
        UAV[4].transform.Translate(targetDirection * UpSpeed * Time.deltaTime, Space.World);

        if (Vector3.Distance(UAV[4].transform.position, targetPosition) < 0.1f)
        {
            currentUAVIndex4 = nextIndex;
        }
    }

    void SetLimitedPersonCount()
    {
        if (groupPersonCount == 2)
        {
            limitedPersonCount = new int[5][]
            {
                new int[3]{ 0, 0, 3 },
                new int[3]{ 0, 0, 3 },
                new int[3]{ 0, 0, 3 },
                new int[3]{ 0, 0, 3 },
                new int[3]{ 0, 0, 3 }
            };
        }
        else if (groupPersonCount == 3)
        {
            limitedPersonCount = new int[5][]
            {
                new int[4]{ 0, 0, 1, 2 },
                new int[4]{ 0, 0, 1, 2 },
                new int[4]{ 0, 0, 1, 2 },
                new int[4]{ 0, 0, 1, 2 },
                new int[4]{ 0, 0, 1, 2 }
            };
        }
        else if (groupPersonCount == 4)
        {
            limitedPersonCount = new int[5][]
            {
                new int[5] { 0, 0, 0, 1, 2 },
                new int[5] { 0, 0, 0, 1, 2 },
                new int[5] { 0, 0, 0, 1, 2 },
                new int[5] { 0, 0, 0, 1, 2 },
                new int[5] { 0, 0, 0, 1, 2 }
            };
        }
        else if (groupPersonCount == 5)
        {
            limitedPersonCount = new int[5][]
            {
                new int[6] { 0, 0, 0, 1, 1, 1 },
                new int[6] { 0, 0, 0, 1, 1, 1 },
                new int[6] { 0, 0, 0, 1, 1, 1 },
                new int[6] { 0, 0, 0, 1, 1, 1 },
                new int[6] { 0, 0, 0, 1, 1, 1 }
            };
        }
        else if (groupPersonCount == 6)
        {
            limitedPersonCount = new int[5][]
            {
                new int[7] { 0, 0, 0, 0, 1, 1, 1 },
                new int[7] { 0, 0, 0, 0, 1, 1, 1 },
                new int[7] { 0, 0, 0, 0, 1, 1, 1 },
                new int[7] { 0, 0, 0, 0, 1, 1, 1 },
                new int[7] { 0, 0, 0, 0, 1, 1, 1 }
            };
        }
    }

    void SetDay2Night()
    {
        RenderSettings.skybox = null;
        gameIsPaused = true;
        RenderSettings.ambientLight = new Color(54f / 255, 68f / 255, 68f / 255);
        float upperBound = Random.value * 360, lowerBound = Random.value * 360;
        if (lowerBound > upperBound) Swap(ref lowerBound, ref upperBound);
        lightHCurve.ResetParameters((upperBound - lowerBound) / 2, (upperBound + lowerBound) / 2);
        upperBound = Random.value * 40;
        lowerBound = Random.value * 40;
        if (lowerBound > upperBound) Swap(ref lowerBound, ref upperBound);
        lightSCurve.ResetParameters((upperBound - lowerBound) / 2, (upperBound + lowerBound) / 2);
        upperBound = Random.value * 30;
        lowerBound = Random.value * 30;
        if (lowerBound > upperBound) Swap(ref lowerBound, ref upperBound);
        lightVCurve.ResetParameters((upperBound - lowerBound) / 2, (upperBound + lowerBound) / 2);

        dayLight.SetActive(false);
        nightLight.SetActive(true);
        roadLights.SetActive(true);
        StartCoroutine(ResumeGame(.5f));
    }

    void SetNight2Day()
    {
        float upperBound = Random.value * 110, lowerBound = Random.value * 110;
        if (lowerBound > upperBound) Swap(ref lowerBound, ref upperBound);
        lightHCurve.ResetParameters((upperBound - lowerBound) / 2, (upperBound + lowerBound) / 2);
        upperBound = Random.value * 40;
        lowerBound = Random.value * 40;
        if (lowerBound > upperBound) Swap(ref lowerBound, ref upperBound);
        lightSCurve.ResetParameters((upperBound - lowerBound) / 2, (upperBound + lowerBound) / 2);
        upperBound = Random.value * 40 + 60;
        lowerBound = Random.value * 40 + 60;
        if (lowerBound > upperBound) Swap(ref lowerBound, ref upperBound);
        lightVCurve.ResetParameters((upperBound - lowerBound) / 2, (upperBound + lowerBound) / 2);

        HandleGlobalLight();

        gameIsPaused = true;

        dayLight.SetActive(true);
        nightLight.SetActive(false);
        roadLights.SetActive(false);
        StartCoroutine(ResumeGame(1f));
    }

    void HandleGlobalLight()
    {
        if (gameIsPaused) return;
        lightHCurve.UpdateX(Time.deltaTime);
        lightSCurve.UpdateX(Time.deltaTime);
        lightVCurve.UpdateX(Time.deltaTime);
        if (currentIsNight)
        {
            RenderSettings.ambientLight = MapHSVTo01(lightHCurve.GetValue(), lightSCurve.GetValue(), lightVCurve.GetValue());
        }
        else
        {
            if (lightHCurve.GetValue() > 40)
            {
                RenderSettings.ambientLight = MapHSVTo01((lightHCurve.GetValue() + 140), lightSCurve.GetValue(), lightVCurve.GetValue());
            }
            else
            {
                RenderSettings.ambientLight = MapHSVTo01(lightHCurve.GetValue(), lightSCurve.GetValue(), lightVCurve.GetValue());
            }
        }
    }

    bool IsValidBoundings(Rect newRect)
    {
        overlapAreas.Add(0);
        for (int i = 0; i < currentBoundings.Count; i++)
        {
            float areaValue = CalculateRectOverlapArea(currentBoundings[i], newRect);
            overlapAreas[i] = Mathf.Max(overlapAreas[i], areaValue);
            overlapAreas[overlapAreas.Count - 1] = Mathf.Max(areaValue, overlapAreas[overlapAreas.Count - 1]);
        }
        currentBoundings.Add(newRect);
        for (int i = 0; i < currentBoundings.Count; i++)
        {
            float overlapPercent = overlapAreas[i] / currentBoundings[i].height / currentBoundings[i].width;
            if (tolerance < 100)
            {
                if (overlapPercent > .7f)
                { // 1f
                    return false;
                }
            }
            else
            {
                if (overlapPercent > .75f)
                { // 1f
                    return false;
                }
            }
        }
        return true;
    }

    float CalculateRectOverlapArea(Rect r1, Rect r2)
    {
        float x1 = r1.xMin, y1 = r1.yMin, x2 = r1.xMax, y2 = r1.yMax, x3 = r2.xMin, y3 = r2.yMin, x4 = r2.xMax, y4 = r2.yMax;
        float x = Mathf.Min(x2, x4) - Mathf.Max(x1, x3), y = Mathf.Min(y2, y4) - Mathf.Max(y1, y3);
        return Mathf.Max(0, x) * Mathf.Max(0, y);
    }

    IEnumerator ResumeGame(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        gameIsPaused = false;
    }

    void SetPersonCountList()
    {
        fbxUsingRange = new Dictionary<int, int[]>();
        personCountList = new List<int>();
        startGroupIds = new Dictionary<int, int>();
        startGroupIds[6] = 0;
        startGroupIds[5] = 2000;
        startGroupIds[4] = 4000;
        startGroupIds[3] = 6500;
        startGroupIds[2] = 9500;
        if (personCountChosen == 0)
        {
            Debug.LogError("Should Choose Person Count in Camera Script!");
            QuitGame();
        }
        if ((personCountChosen & PersonCountEnumUAV.p2) == PersonCountEnumUAV.p2)
        {
            personCountList.Add(2);
        }
        if ((personCountChosen & PersonCountEnumUAV.p3) == PersonCountEnumUAV.p3)
        {
            personCountList.Add(3);
        }
        if ((personCountChosen & PersonCountEnumUAV.p4) == PersonCountEnumUAV.p4)
        {
            personCountList.Add(4);
        }
        if ((personCountChosen & PersonCountEnumUAV.p5) == PersonCountEnumUAV.p5)
        {
            personCountList.Add(5);
        }
        if ((personCountChosen & PersonCountEnumUAV.p6) == PersonCountEnumUAV.p6)
        {
            personCountList.Add(6);
        }
        fbxUsingRange[2] = new int[2] { 1, 200 };
        fbxUsingRange[3] = new int[2] { 201, 500 };
        fbxUsingRange[4] = new int[2] { 501, 900 };
        fbxUsingRange[5] = new int[2] { 901, 1900 };
        fbxUsingRange[6] = new int[2] { 1901, 2500 };
        groupPersonCount = personCountList[0];
    }

    void SaveJpgThreadFunction()
    {
        while (true)
        {
            while (saveJpgBuffer.Count > 0)
            {
                File.WriteAllBytes(datasetImagesPath + "/" + saveJpgBuffer[0].Item1, saveJpgBuffer[0].Item2);
                saveJpgBuffer.RemoveAt(0);
            }
            Thread.Sleep(100);
        }
    }

    IEnumerator AddAnimator()
    {
        yield return new WaitForSeconds(1f);
        foreach (GameObject person in scenePersons)
        {
            person.AddComponent<Animator>();
            person.GetComponent<Animator>().runtimeAnimatorController = walkController;
        }
    }

    void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }

    string Round2(float a)
    {
        return System.Math.Round(a, 2).ToString();
    }
    string Round6(float a)
    {
        return System.Math.Round(a, 6).ToString();
    }
    float Round4Float(float a)
    {
        return (float)System.Math.Round(a, 4);
    }
    string Round4String(float a)
    {
        return System.Math.Round(a, 4).ToString();
    }

    string IntAddZeros(int a)
    {
        string temps = a.ToString();
        return new string('0', 7 - temps.Length) + temps;

    }

    Color MapHSVTo01(float H, float S, float V)
    {
        return Color.HSVToRGB(H / 360, S / 100, V / 100);
    }

    private void OnDrawGizmos()
    {
        return;
        Gizmos.color = Color.red;
        if (Application.isPlaying)
        {
            Gizmos.DrawLine(cubeEdgePositions[0], cubeEdgePositions[1]);
            Gizmos.DrawLine(cubeEdgePositions[1], cubeEdgePositions[2]);
            Gizmos.DrawLine(cubeEdgePositions[2], cubeEdgePositions[3]);
            Gizmos.DrawLine(cubeEdgePositions[3], cubeEdgePositions[0]);
            Gizmos.DrawLine(cubeEdgePositions[4], cubeEdgePositions[5]);
            Gizmos.DrawLine(cubeEdgePositions[5], cubeEdgePositions[6]);
            Gizmos.DrawLine(cubeEdgePositions[6], cubeEdgePositions[7]);
            Gizmos.DrawLine(cubeEdgePositions[7], cubeEdgePositions[4]);
            Gizmos.DrawLine(cubeEdgePositions[8], cubeEdgePositions[9]);
            Gizmos.DrawLine(cubeEdgePositions[9], cubeEdgePositions[10]);
            Gizmos.DrawLine(cubeEdgePositions[10], cubeEdgePositions[11]);
            Gizmos.DrawLine(cubeEdgePositions[11], cubeEdgePositions[8]);
        }
    }

    void QuitGame()
    {
        if (Application.isEditor) EditorApplication.ExitPlaymode();
        else Application.Quit();
    }
}

class SineCurveUAV
{
    float A;
    float T;
    //float phi;
    float B;
    float runtimeRatio;
    public float x;
    public SineCurveUAV(float _A = 0, float _B = 0, float _runtimeRatio = 1)
    {
        runtimeRatio = _runtimeRatio;
        ResetParameters(_A, _B);
    }

    public float GetValue()
    {
        return A * Mathf.Sin(2 * Mathf.PI / T * x) + B;
    }

    public void SetStartX()
    {
        x = Random.value * T;
    }
    public void UpdateX(float deltaTime)
    {
        x += deltaTime;
    }

    public void ResetParameters(float _A = 0, float _B = 0)
    {
        T = (Random.value + 1f) * 1000f * runtimeRatio;

        A = _A;
        B = _B;
        SetStartX();
    }

    void Swap<T>(ref T a, ref T b)
    {
        T temp = a;
        a = b;
        b = temp;
    }
}

class JpgSaverUAV
{
    public string jpgPath;
    public byte[] jpgBytes;
    public JpgSaverUAV(string _path, byte[] _bytes)
    {
        jpgPath = _path;
        jpgBytes = new byte[_bytes.Length];
        _bytes.CopyTo(jpgBytes, 0);
    }
    ~JpgSaverUAV()
    {

    }
    public void SaveJpg()
    {
        File.WriteAllBytes(jpgPath, jpgBytes);
    }
}

public enum PersonCountEnumUAV
{
    p2 = 1,
    p3 = 2,
    p4 = 4,
    p5 = 8,
    p6 = 16
}
