using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using NXOpen;
using NXOpen.UF;
using NXOpen.Features;
using NXOpen.Drawings;
using NXOpen.Annotations;

namespace SemtecConnector
{
    public class VideoPin
    {
        public static Session theSession = Session.GetSession();
        public static UFSession theUFSession = UFSession.GetUFSession();
        public static UI theUI = UI.GetUI();
        public static ListingWindow lw = theSession.ListingWindow;
        public static Part workPart = theSession.Parts.Work;

        private static double pitchOfPins = 1.27;
        private static double pinDiameter = 0.4064;
        private static double hortizontalPitch = 1.016;
        private static double VerticalPitch = 1.27;
        private static double wallThickness = 0.889;
        private static double height = 3.7846;
        private static double length, breadth;

        public static void Main (string[] args)
        {
            lw.Open();

            // Undo Mark for the Add component
            NXOpen.Session.UndoMarkId markId1;
            markId1 = theSession.SetUndoMark(NXOpen.Session.MarkVisibility.Visible, "Connector Pin Model");
                        
            // Check for Work Part is open or not
            if (workPart!= null)
            {
                // Calling Function to Create 3D model for Connector
                ConnectorUI_Styler.ConnectorMain();
                // Calling Function to Create drawing for Connector
                ConectorDrawing.CreateDrawing();
            }
            else
            {
                lw.WriteLine("Part file is not Opened");
            }

            // Undo mark
            theSession.SetUndoMarkName(markId1, "Remove Connector Pin Model");

            // Orient the View to Isometric View
            //workPart.ModelingViews.WorkView.Orient(NXOpen.View.Canned.Isometric, NXOpen.View.ScaleAdjustment.Fit);
        }

        public static int GetUnloadOption(string dummy)
        {
            return (int)NXOpen.Session.LibraryUnloadOption.Immediately;
        }

        public static void CreateConnector(int noOfXPins, int noOfYPins)
        {
            int xPins = noOfXPins;
            int yPins = noOfYPins;
            
            // To collect the any Objects or Bodies in the Work Part
            NXOpen.DisplayableObject[] objects1 = workPart.Bodies.ToArray();
            NXOpen.DisplayableObject[] objects2 = workPart.Sketches.ToArray();

            // Check for Bodies are in workpart before running Dll File
            if (objects1.Length >= 1)
            {
                bool notifyOnDelete1;
                notifyOnDelete1 = theSession.Preferences.Modeling.NotifyOnDelete;

                theSession.UpdateManager.ClearErrorList();

                int nErrs1;
                nErrs1 = theSession.UpdateManager.AddObjectsToDeleteList(objects1);

                int nErrs2;
                nErrs2 = theSession.UpdateManager.AddObjectsToDeleteList(objects2);

                bool notifyOnDelete2;
                notifyOnDelete2 = theSession.Preferences.Modeling.NotifyOnDelete;

                //// Collect all the Object and hide them
                //theSession.DisplayManager.BlankObjects(objects1);
                //workPart.ModelingViews.WorkView.FitAfterShowOrHide(NXOpen.View.ShowOrHideType.HideOnly);
            }
            // Check for Sketchs are in workpart before running Dll File
            if (objects2.Length >= 1)
            {
                bool notifyOnDelete1;
                notifyOnDelete1 = theSession.Preferences.Modeling.NotifyOnDelete;

                theSession.UpdateManager.ClearErrorList();

                int nErrs1;
                nErrs1 = theSession.UpdateManager.AddObjectsToDeleteList(objects2);

                bool notifyOnDelete2;
                notifyOnDelete2 = theSession.Preferences.Modeling.NotifyOnDelete;
            }

            // Calling the CalculateDimensionsOfConnector function to calculate the length and breadth
            // of a connector based on number of pin need in X and Y Direction
            CalculateDimensionsOfConnector(xPins, yPins, out length, out breadth);

            // Base point to Create the Base Block of connector
            Point3d point = new Point3d(-length / 2, -breadth / 2, 0);
            // calling CreateBlock Function
            CreateBlock(point, length, breadth, height, out Feature blockFeature);

            // Declaration and assign Connector base body to it 
            Body connectorBaseBody = null;

            // Looping through the Bodies in workPart
            foreach (Body body in workPart.Bodies)
            {
                // Conditon to check body not contain these two colors
                if (body.Color != 201 && body.Color != 4)
                    connectorBaseBody = body; // Assign body to connectorBaseBody
            }

            // Calling the ChangeColor Function to change the color of the Connector base
            ChangeColor(connectorBaseBody, 201);

            // Declaration of shellFace, sideCutFace and List to collect Faces
            Face shellFace = null;
            List<Face> bottomChamferFaces = new List<Face>();
            List<Edge> bottomChamferEdges = new List<Edge>();
            Face sideCutFace = null;

            // looping through the faces of the connectorBaseBody 
            foreach (Face face in connectorBaseBody.GetFaces())
            {
                // Condition for finding Planar faces of body
                if (face.SolidFaceType == Face.FaceType.Planar)
                {
                    // Declaration of the array for Face Direction
                    double[] faceDirection;
                    // Calling the FindFaceDirection Function
                    FindFaceDirection(face, out faceDirection);
                    // Condition for Required Face in +ve Z direction
                    if (faceDirection[2] == 1)
                    {
                        shellFace = face; //Assign faces to shellFace
                        continue;
                    }
                    // Condition for Required Face in -ve and +ve Y direction and -ve Z direction
                    else if (faceDirection[1] == -1 || faceDirection[1] == 1 || faceDirection[2] == -1)
                    {
                        bottomChamferFaces.Add(face); // Add Y and Z direction face to sideYFaces
                    }
                    // Condition for Required Face in -ve Y direction
                    if (faceDirection[1] == -1)
                    {
                        sideCutFace = face; //Assign faces to sideCutFace
                    }
                }
            }

            // Calling CommonEdgesOfFaces function to find Common edges in given Face list
            CommonEdgesOfFaces(bottomChamferFaces, out bottomChamferEdges);

            // calling CreateChamfer function to create chamfer on bottom face
            CreateChamfer(bottomChamferEdges, 0.762, out Feature chamferFeature);

            // Calling the GetDatumAxis Function to Get the Y-Axis for Top Face Skecth
            GetDatumAxis("Y", out DatumAxis faceSketchNormal);

            // Calling the CreateSketch to Skecth on Top Face of Connector Base
            CreateSketch(shellFace, faceSketchNormal, "Face Sketch", out Sketch extrudeCutSketch, out Feature extrudeCutSketchfeture);

            // Calling Function to Create Geometry of Top Skeck
            AddCutGeomentry(extrudeCutSketch);

            // Calling function to Extrude the extrudeCutSketch
            SketchExtrudeBoolean(connectorBaseBody, extrudeCutSketchfeture, extrudeCutSketch, 0, 2, false, true, out Feature feature);
            ////CreateShell(connectorBaseBody, shellFace, wallThickness);


            // Declaration of Dictionary to Collect the face in X, Y and Z Directions
            Dictionary<Face, double> xDirectionFaces = new Dictionary<Face, double>();
            Dictionary<Face, double> yDirectionFaces = new Dictionary<Face, double>();
            Dictionary<Face, double> zDirectionFaces = new Dictionary<Face, double>();

            // Looping through the All the Faces of the connector Base
            foreach (Face face in connectorBaseBody.GetFaces())
            {
                // Chack for Solid Planar Faces of the Connector Base
                if (face.SolidFaceType == Face.FaceType.Planar)
                {
                    double[] faceDirection; // Declaraton of face Direction Array
                    double[] facePoint; // Declaraton of face point array
                                        // Calling the FindFaceDirection function to find the direction of the faces
                    FindFaceDirection(face, out faceDirection, out facePoint);

                    // Condition for Required Face in -ve and +ve X direction
                    if (faceDirection[0] == 1 || faceDirection[0] == -1)
                    {
                        xDirectionFaces.Add(face, facePoint[0]); // Add X Direction Faces
                    }
                    // Condition for Required Face in -ve and +ve Y direction
                    else if (faceDirection[1] == 1 || faceDirection[1] == -1)
                    {
                        yDirectionFaces.Add(face, facePoint[1]); // Add Y Direction Faces
                    }
                    // Condition for Required Face in -ve and +ve Z direction
                    else if (faceDirection[2] == 1)
                    {
                        zDirectionFaces.Add(face, facePoint[2]); // Add Z Direction Faces
                    }
                }
            }

            // Order the All the face based on their position in respective Direction
            xDirectionFaces = xDirectionFaces.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            yDirectionFaces = yDirectionFaces.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            zDirectionFaces = zDirectionFaces.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            // Remove the unwanted Faces from the Dictionary of their direction
            xDirectionFaces.Remove(xDirectionFaces.Keys.First());
            xDirectionFaces.Remove(xDirectionFaces.Keys.Last());
            yDirectionFaces.Remove(yDirectionFaces.Keys.First());
            yDirectionFaces.Remove(yDirectionFaces.Keys.Last());
            zDirectionFaces.Remove(zDirectionFaces.Keys.First());

            // Collecting all the required faces to Lists from Dictionary Faces
            List<Face> topChamferFaces = zDirectionFaces.Keys.ToList();
            List<Face> xChamferFaces = xDirectionFaces.Keys.ToList();
            List<Face> yChamferFaces = new List<Face>();
            List<Face> allChamferFaces = new List<Face>();

            // Collecting the required faces to List from yDirectionFaces
            yChamferFaces.AddRange(new Face[] { yDirectionFaces.Keys.First(), yDirectionFaces.Keys.Last() });

            // Adding All Required X and Y Direction faces to allChamferFaces List
            allChamferFaces.AddRange(xChamferFaces);
            allChamferFaces.AddRange(yChamferFaces);

            // Declaration of the List for Chamfer Edges
            List<Edge> allChamferEdges = new List<Edge>();

            // Calling Function CommonEdgesOfFaces
            CommonEdgesOfFaces(topChamferFaces, allChamferFaces, out allChamferEdges);

            // calling CreateChamfer function to create chamfer on bottom face
            CreateChamfer(allChamferEdges, 0.4, out Feature topChamferFeature);

            // Calling the GetDatumAxis Function to Get the Y-Axis for Top Face Skecth
            GetDatumAxis("Z", out DatumAxis sideSketchNormal);

            // Calling CreateSketch to create the side on the Connector Base
            CreateSketch(sideCutFace, sideSketchNormal, "SideSkecth", out Sketch sideCutSketch, out Feature sideSkecthFeature);

            // Calling AddSideCutGeomentry 
            AddSideCutGeomentry(sideCutSketch);

            // Calling SketchExtrudeBoolean to Extrude required Skecth
            SketchExtrudeBoolean(connectorBaseBody, sideSkecthFeature, sideCutSketch, 0, 0.4318, false, true, out Feature sideExtrudeFeature);

            // Feature array to Mirror 
            Feature[] sideMirrorFeatures = { sideExtrudeFeature };
            // Declaration of mirrorPlane
            DatumPlane mirrorPlane = null;
            // Calling GetDatumPlane Function to Get XZ Plane
            GetDatumPlane("XZ", out mirrorPlane);
            // Calling MirrorFeature Function
            MirrorFeature(sideMirrorFeatures, mirrorPlane, out Feature mirrorFeature);

            // Feature Array to Pattern
            Feature[] sideCutPattern = { sideExtrudeFeature, mirrorFeature };

            // Declaration Vector3d in X and Y direction for Linner Pattern
            Vector3d xVector = new Vector3d(1, 0, 0);
            Vector3d yVector = new Vector3d(0, 1, 0);
            // Declaration numberOfCopies in pattern based on Length of the Connector
            int numberOfCopies = (int)((length - (2 * pitchOfPins)) / (2 * pitchOfPins));
            // Calling LinearPattern Function
            LinearPattern(sideCutPattern, xVector, numberOfCopies, (2 * pitchOfPins), yVector);


            ///*
            // Declaration of Datum Plane to Sketch the Pin in Connector
            DatumPlane sketchDatumPlane = null;
            // Calling the GetDatumPlane Function
            GetDatumPlane("XY", out sketchDatumPlane);

            // Calling the CreateSketch Function
            CreateSketch(sketchDatumPlane, out Sketch pinSketch, out NXOpen.Features.Feature sketchFeature);
            // Calling the AddPinGeomentry Function
            AddPinGeomentry(pinSketch);
            // Calling the SketchExtrude Function
            SketchExtrude(sketchFeature, pinSketch, 1.7848, 3.3528, false, out Feature extrudeFeature);

            // Calling the CreateSketch Function 
            CreateSketch(sketchDatumPlane, out Sketch baseCircle1Sketch, out Feature baseCircle1Feature);
            // Calling the AddBaseCircle1Geomentry Function
            AddBaseCircle1Geomentry(baseCircle1Sketch);
            // Calling the SketchExtrude Function
            SketchExtrude(baseCircle1Feature, baseCircle1Sketch, 0, 0.5, true, out Feature baseCircle1ExtrudeFeature);

            // Declaration of baseCircle2Sketch and their Features
            Sketch baseCircle2Sketch = null;
            Feature baseCircle2Feature = null, baseCircle2ExtrudeFeature = null;
            // Check for yPins direction more than 1
            if (yPins != 1)
            {
                // Calling the CreateSketch Function 
                CreateSketch(sketchDatumPlane, out baseCircle2Sketch, out baseCircle2Feature);
                // Calling the AddBaseCircle2Geomentry Function 
                AddBaseCircle2Geomentry(baseCircle2Sketch);
                // Calling the SketchExtrude Function 
                SketchExtrude(baseCircle2Feature, baseCircle2Sketch, 0, 0.5, true, out baseCircle2ExtrudeFeature);
            }

            // Calling the CreateSketch Function
            CreateSketch(sketchDatumPlane, out Sketch baseSeat1Sketch, out Feature baseSeat1Feature);
            // Calling the AddBaseSeat1Geomentry Function
            AddBaseSeat1Geomentry(baseSeat1Sketch);
            // Calling the SketchExtrudeBoolean Function
            SketchExtrudeBoolean(connectorBaseBody, baseSeat1Feature, baseSeat1Sketch, 0, 0.5, true, true, out Feature baseSeat1ExtrudeFeature);

            // Calling the CreateSketch Function
            CreateSketch(sketchDatumPlane, out Sketch baseSeat2Sketch, out Feature baseSeat2Feature);
            // Calling the AddBaseSeat2Geomentry Function
            AddBaseSeat2Geomentry(baseSeat2Sketch);
            // Calling the SketchExtrudeBoolean Function
            SketchExtrudeBoolean(connectorBaseBody, baseSeat2Feature, baseSeat2Sketch, 0, 0.5, true, true, out Feature baseSeat2ExtrudeFeature);

            // Check for yPins direction more than 1
            if (yPins != 1)
            {
                // Hide the Extruded Sketch of the Pin Connector
                NXOpen.DisplayableObject[] extrudedSketch = { extrudeCutSketch, sideCutSketch, pinSketch, baseSeat1Sketch, baseSeat2Sketch, baseCircle1Sketch, baseCircle2Sketch };
                theSession.DisplayManager.BlankObjects(extrudedSketch);
            }
            else
            {
                // Hide the Extruded Sketch of the Pin Connector
                NXOpen.DisplayableObject[] extrudedSketch = { extrudeCutSketch, sideCutSketch, pinSketch, baseSeat1Sketch, baseSeat2Sketch, baseCircle1Sketch };
                theSession.DisplayManager.BlankObjects(extrudedSketch);
            }

            // Calling LinearPattern Function for different Features of Pins and Base supports
            LinearPattern(baseSeat1ExtrudeFeature, yVector, yPins, pitchOfPins, xVector);
            LinearPattern(baseSeat2ExtrudeFeature, xVector, xPins, pitchOfPins, yVector);
            LinearPattern(baseCircle1ExtrudeFeature, yVector, yPins, pitchOfPins, xVector, xPins - 1, pitchOfPins);
            // Check for yPins direction more than 1
            if (yPins != 1)
            {
                LinearPattern(baseCircle2ExtrudeFeature, yVector, yPins - 1, pitchOfPins, xVector, xPins, pitchOfPins);
            }

            // Looping through the bodies in workpart 
            foreach (Body body in workPart.Bodies)
            {
                // Check for uncolor body 
                if (body != connectorBaseBody && body.Color != 201 && body.Color != 4)
                {
                    // Array to collect faces of uncolor body 
                    Face[] pinFaces = body.GetFaces();
                    // Change the color
                    ChangeColor(pinFaces, 4);
                }
            }

            // Declaration of the patternFeatures Array and assign extrudeFeature
            Feature[] patternFeatures = { extrudeFeature };

            // Calling LinearPattern Function
            LinearPattern(patternFeatures, xVector, xPins, pitchOfPins, yVector, yPins, pitchOfPins);
            //*/

            // Orient the View to Isometric View
            workPart.ModelingViews.WorkView.Orient(NXOpen.View.Canned.Isometric, NXOpen.View.ScaleAdjustment.Fit);

        }

        private static void CalculateDimensionsOfConnector(int numberOfPins_X, int numberOfPins_Y, out double length, out double breadth)
        {
            length = pitchOfPins * (numberOfPins_X - 1) + 2 * hortizontalPitch + 2 * wallThickness;
            breadth = pitchOfPins * (numberOfPins_Y - 1) + 2 * VerticalPitch + 2 * wallThickness;
        }
        /// <summary>
        /// To Create Block
        /// </summary>
        /// <param name="point">Center Point</param>
        /// <param name="length">Length of Block</param>
        /// <param name="Width"> width of Block</param>
        /// <param name="Height">Height of Block</param>
        /// <param name="feature">Out as Feature</param>
        /// <returns></returns>
        private static bool CreateBlock(Point3d point, double length, double Width, double Height, out NXOpen.Features.Feature feature)
        {
            feature = null; // out condition always decleare at start of the try catch
            try
            {
                // Declare and Initialize Feature Class Object
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                // Declare BlockFeatureBuilder class Object
                NXOpen.Features.BlockFeatureBuilder blockFeatureBuilder;

                //Initialize Object for BlockFeature
                blockFeatureBuilder = workPart.Features.CreateBlockFeatureBuilder(nullNXOpen_Features_Feature);

                // Set Boolean Option
                blockFeatureBuilder.BooleanOption.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Create;

                // BlockFeatureBuilder type
                blockFeatureBuilder.Type = NXOpen.Features.BlockFeatureBuilder.Types.OriginAndEdgeLengths;

                // Pass Orgin point & the Dimenions of the Block
                blockFeatureBuilder.SetOriginAndLengths(point, length.ToString(), Width.ToString(), Height.ToString());

                feature = blockFeatureBuilder.CommitFeature(); // Commits the feature parameters and Creates the Feature

                blockFeatureBuilder.Destroy(); // Destroy the Builder. It deletes the builder, and the cleans up any objects create by the Builder  

                return true;
            }
            catch (Exception ex)
            {                
                throw;
                return false;
            }
        }
        /// <summary>
        /// To Perform the Shell Operation
        /// </summary>
        /// <param name="body">On Which Body</param>
        /// <param name="face">On Which Face</param>
        /// <param name="wallThick">Wall Thickness</param>
        /// <returns></returns>
        private static bool CreateShell(Body body, Face face, double wallThick)
        {
            try
            {
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                NXOpen.Features.ShellBuilder shellBuilder1;
                shellBuilder1 = workPart.Features.CreateShellBuilder(nullNXOpen_Features_Feature);

                shellBuilder1.Tolerance = 0.01;

                shellBuilder1.UseSurfaceApproximation = true;

                shellBuilder1.TgtPierceOption = false;

                shellBuilder1.SetDefaultThickness(wallThick.ToString());

                shellBuilder1.Body = body;

                NXOpen.ScCollector scCollector1;
                scCollector1 = workPart.ScCollectors.CreateCollector();

                NXOpen.Face[] faces1 = { face };
                NXOpen.FaceDumbRule faceDumbRule1;
                faceDumbRule1 = workPart.ScRuleFactory.CreateRuleFaceDumb(faces1);

                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = faceDumbRule1;
                scCollector1.ReplaceRules(rules1, false);

                shellBuilder1.RemovedFacesCollector = scCollector1;

                NXOpen.NXObject nXObject1;
                nXObject1 = shellBuilder1.Commit();

                shellBuilder1.Destroy();

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// To Find the Direction of the given Face
        /// </summary>
        /// <param name="face">Pass the Face for which need to find the Direction</param>
        /// <param name="direction">Out Dirction as Double Array of Size 3</param>
        /// <returns></returns>
        private static bool FindFaceDirection(Face face, out double[] direction)
        {
            direction = new double[3]; // Declaration of direction Array
            /* In Direction [] Array 
                * If Direction [0] and equal to the "1" means X Direction in +ve side
                * If Direction [0] and equal to the "-1" means X Direction in -ve side
                * If Direction [1] and equal to the "1" means Y Direction in +ve side
                * If Direction [1] and equal to the "-1" means Y Direction in -ve side                 * 
                * If Direction [2] and equal to the "1" means Z Direction in +ve side
                * If Direction [2] and equal to the "-1" means Z Direction in -ve side

               eg: if (Math.Round(FaceDir[1], 1) == 1)
                       {ReqFaceFillet = faces;}
            */
            try
            {
                int type; // Declaration of Type
                double[] point = new double[3]; // Declaration of Point Array
                /*
                 * box[0] = Xmin
                 * box[1] = Ymin
                 * box[2] = Zmin
                 * box[3] = Xmax
                 * box[4] = Ymax
                 * box[5] = Zmax
                 */
                double[] box = new double[6]; // Declaration of box Array
                double radius; // Declaration of Radius
                double rad_date; // Declaration of Radial Data
                int nor_dir; // Declaration of Normal Direction of Face
                // Ufunc to get the Face Data
                theUFSession.Modl.AskFaceData(face.Tag, out type, point, direction, box, out radius, out rad_date, out nor_dir);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// To Find the Direction of the given Face
        /// </summary>
        /// <param name="face">Pass the Face for which need to find the Direction</param>
        /// <param name="direction">Out Dirction as Double Array of Size 3</param>
        ///  <param name="point">Out Point as Double Array of Size 3</param>
        /// <returns></returns>
        private static bool FindFaceDirection(Face face, out double[] direction, out double [] point)
        {
            direction = new double[3]; // Declaration of direction Array
            point = new double[3]; // Declaration of point Array
            /* In Direction [] Array 
                * If Direction [0] and equal to the "1" means X Direction in +ve side
                * If Direction [0] and equal to the "-1" means X Direction in -ve side
                * If Direction [1] and equal to the "1" means Y Direction in +ve side
                * If Direction [1] and equal to the "-1" means Y Direction in -ve side                 * 
                * If Direction [2] and equal to the "1" means Z Direction in +ve side
                * If Direction [2] and equal to the "-1" means Z Direction in -ve side

               eg: if (Math.Round(FaceDir[1], 1) == 1)
                       {ReqFaceFillet = faces;}
            */
            try
            {
                int type; // Declaration of Type
                //double[] point = new double[3]; // Declaration of Point Array
                /*
                 * box[0] = Xmin
                 * box[1] = Ymin
                 * box[2] = Zmin
                 * box[3] = Xmax
                 * box[4] = Ymax
                 * box[5] = Zmax
                 */
                double[] box = new double[6]; // Declaration of box Array
                double radius; // Declaration of Radius
                double rad_date; // Declaration of Radial Data
                int nor_dir; // Declaration of Normal Direction of Face
                // Ufunc to get the Face Data
                theUFSession.Modl.AskFaceData(face.Tag, out type, point, direction, box, out radius, out rad_date, out nor_dir);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// To Get the Required Datum Plane from the Datum Coordinate System
        /// </summary>
        /// <param name="planeName"> XY- DatumPlane, YZ- DatumPlane, XZ - DatumPlane </param>
        /// <param name="datumPlane">out as Required Datum Plane</param>
        /// <returns></returns>
        private static bool GetDatumPlane (string planeName, out DatumPlane datumPlane)
        {
            datumPlane = null;
            try
            {
                // Looping through the all Datums in the work Part
                foreach (var plane in workPart.Datums)
                {
                    // Check Datum Planes
                    if (plane.GetType().ToString() == "NXOpen.DatumPlane")
                    {
                        // Declaration of Datum Plane
                        DatumPlane requiredDatumPlane = (DatumPlane)plane;
                        if (planeName.Contains("XY"))
                        {
                            // Check for the "XY" Datum Plane from the Datums
                            if (requiredDatumPlane.Normal.X == 0 && requiredDatumPlane.Normal.Y == 0)
                            {
                                // Assign XY Datum plane to datumPlane
                                datumPlane = requiredDatumPlane;
                            }
                        }
                        else if (planeName.Contains("YZ"))
                        {
                            // Check for the "YZ" Datum Plane from the Datums
                            if (requiredDatumPlane.Normal.Y == 0 && requiredDatumPlane.Normal.Z == 0)
                            {
                                // Assign YZ Datum plane to datumPlane
                                datumPlane = requiredDatumPlane;
                            }
                        }
                        else
                        {
                            // Check for the "XZ" Datum Plane from the Datums
                            if (requiredDatumPlane.Normal.X == 0 && requiredDatumPlane.Normal.Z == 0)
                            {
                                // Assign XZ Datum plane to datumPlane
                                datumPlane = requiredDatumPlane;
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// To Get the Required Datum Axis from the Datum Coordinate System
        /// </summary>
        /// <param name="axisName">X - Axis, Y - Axis, Z - Axis</param>
        /// <param name="datumAxis">out as Required Datum Axis</param>
        /// <returns></returns>
        private static bool GetDatumAxis(string axisName, out DatumAxis datumAxis)
        {
            datumAxis = null;
            try
            {
                // Collecting the Datum from WorkPart Session
                DatumCollection datumsData = workPart.Datums; 

                // Get Datum Axis from the WorkCoordinateSystem (WCS)                
                foreach (var items in datumsData)
                {
                    /* This statement is also used for same application
                     * if ((items.GetType().ToString() == "NXOpen.DatumAxis") // Condition to Get DatumAxis
                     */
                    if (items.GetType() == typeof(DatumAxis)) // Condition to Get DatumAxis
                    {
                        DatumAxis requiredDatumAxis = (DatumAxis)items;

                        if (axisName.Contains("X"))
                        {
                            //Condition to Get X Axis
                            if (requiredDatumAxis.Direction.X == 1)
                                datumAxis = requiredDatumAxis;
                        }
                        else if (axisName.Contains("Y"))
                        {
                            //Condition to Get Y Axis
                            if (requiredDatumAxis.Direction.Y == 1) 
                                datumAxis = requiredDatumAxis;
                        }
                        else
                        {
                            //Condition to Get Z Axis
                            if (requiredDatumAxis.Direction.Z == 1) 
                                datumAxis = requiredDatumAxis;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// To Create Sketch on Selected Plane using Sketch InPlace Builder
        /// </summary>
        /// <param name="datumPlane">Pass the Datum Plane on which Sketch to Create</param>
        /// <param name="sketch">Out as Skecth in Passed Datum Plane</param>
        /// <param name="sketchFeature">Out as Skecth Feature</param>
        /// <returns></returns>
        private static bool CreateSketch(DatumPlane datumPlane, out Sketch sketch, out NXOpen.Features.Feature sketchFeature)
        {
            sketchFeature = null;
            sketch = null;
            try
            {
                // Declaration Sketch In Place Builder
                NXOpen.SketchInPlaceBuilder sketchBuilder1 = workPart.Sketches.CreateSketchInPlaceBuilder2(null);

                // Declaration of Direction Vector to rotate the Sketch
                Vector3d directionVector = new Vector3d();
                if (datumPlane.JournalIdentifier.Contains("XY")) // Check for XY Datum Palne
                {
                    directionVector = new Vector3d(1, 0, 0); // Assign X Direction Vector to Rotate the Sketch
                }
                else if (datumPlane.JournalIdentifier.Contains("YZ")) // Check for YZ Datum Palne
                {
                    directionVector = new Vector3d(0, 1, 0); // Assign Y Direction Vector to Rotate the Sketch
                }
                else // Check for XZ Datum Palne
                {
                    directionVector = new Vector3d(0, 0, 1);  // Assign Z Direction Vector to Rotate the Sketch
                }

                // Declarartion of Normal Vector to Sketching Plane
                Vector3d normalVector = datumPlane.Normal;

                // Declaration and Create the Origin point for Sketch
                NXOpen.Point originPoint = workPart.Points.CreatePoint(datumPlane.Origin);

                sketchBuilder1.SketchOrigin = originPoint; // Assign Origin Point to Sketch Builder

                // Create the Sketch Reference Palne and Assign to Sketch Builder
                sketchBuilder1.PlaneReference = workPart.Planes.CreatePlane(datumPlane.Origin, normalVector, SmartObject.UpdateOption.Mixed);

                // Create the Sketch Reference Axis and Assign to Sketch Builder
                sketchBuilder1.AxisReference = workPart.Directions.CreateDirection(originPoint, directionVector);

                // Assign Inferred Option to Sketch Builder
                sketchBuilder1.PlaneOption = NXOpen.Sketch.PlaneOption.Inferred;

                // Commit the Sketch Builder
                sketch = (NXOpen.Sketch)sketchBuilder1.Commit();

                sketchBuilder1.Destroy(); // Destroy the Sketch Builder

                //Active the Sketch 
                sketch.Activate(Sketch.ViewReorient.True);

                // Looping Through the all Feature in work Part
                foreach (NXOpen.Features.Feature feature in workPart.Features)
                {
                    // Check for Feature is Sketch 
                    if (feature.FeatureType.ToLower().Contains("sketch"))
                    {
                        // Array to Collect the all Entities in Sketch Feature
                        NXObject[] sketchObjects = feature.GetEntities();

                        // Check for sketchObjects array is equal to one
                        // Because No Entity is added to Sketch other than display Entity, 
                        if (sketchObjects.Length == 1)
                        {
                            // Assign Feature Sketch to sketchFeature
                            sketchFeature = feature; 
                            break; // exist the loop
                        }
                    }
                }

                return true;

            }
            catch (Exception ex)
            {
                theUI.NXMessageBox.Show("Exception", NXMessageBox.DialogType.Error, ex.ToString());                
                throw;
                return false;

            }

        }

        /// <summary>
        /// To Create Sketch on Selected Face 
        /// </summary>
        /// <param name="sketchFace">Pass the Face on which Sketch to Create</param>
        /// <param name="datumAxis">In direction to Selected Face</param>
        /// <param name="sketchName">Sketch Name</param>
        /// <param name="sketch">Out as Skecth in Passed Face</param>
        /// <param name="sketchFeature">Out as Skecth Feature</param>
        /// <returns></returns>
        private static bool CreateSketch(Face sketchFace, DatumAxis datumAxis, string sketchName,out Sketch sketch, out NXOpen.Features.Feature sketchFeature)
        {
            sketchFeature = null;
            try
            {
                // Start of the Sketch on Face of Block Code using UF is "theUFSession.Sket.CreateSketch()" 
                foreach(Sketch sketchs in workPart.Sketches)
                {
                    if (sketchs.JournalIdentifier.ToUpper() == sketchName.ToUpper())
                    {
                        SketchCollection sketchCollection = workPart.Sketches; // get the Sketch from Session
                        double sketchCount = sketchCollection.ToArray().Length; // No of Sketch in Session

                        sketchName = sketchName + sketchCount.ToString(); // Name of the Sketch
                        break;
                    }
                }
                
                string Name1 = sketchName; // Sketch Name
                // Initialize the SketchTag by Name
                theUFSession.Sket.InitializeSketch(ref Name1, out Tag tag01); 

                // Just Declaration not need for Option 1
                double[] matrixs = new double[0];  

                //Tag[] objects = { faceskt.Tag, reqEdge[0].Tag }; // Decleration of Tag Array example
                // Declaration & Assign values to Tag Object
                Tag[] objects = new Tag[2];  

                // Assign the face on which Sketch to Create ([0]: Solid face/Datum plane object)
                objects[0] = sketchFace.Tag; 

                // Assign Axis that is Normal to Sketching Face or Plane
                // ([1]: Reference object (edge, datum axis, solid face/datum)
                objects[1] = datumAxis.Tag; 

                // [0]: Reference edge 1: Horizontal 2: Vertical & [1]: Direction 1: Start to end (from vertex1 to vertex2)
                // -1: End to start (from vertex2 to vertex1)
                int[] Reff = { 2, 1 }; 

                theUFSession.Sket.CreateSketch(Name1, 1, matrixs, objects, Reff, 1, out Tag faceSketchTag); // Sketch UF to Create Sketch
                //theSession.ActiveSketch.Activate(Sketch.ViewReorient.True); // To rotate the Sketch to Normal View
                // End of the New Sketch Code using UF is "theUFSession.Sket.CreateSketch()" 

                // Code for Coverting Tag to NX Object
                sketch = (Sketch) NXOpen.Utilities.NXObjectManager.Get(faceSketchTag);

                //Active the Sketch
                sketch.Activate(Sketch.ViewReorient.True);

                //theSession.ActiveSketch.Deactivate(NXOpen.Sketch.ViewReorient.True, NXOpen.Sketch.UpdateLevel.Model);

                // Looping Through the all Feature in work Part
                foreach (NXOpen.Features.Feature feature in workPart.Features)
                {
                    // Check for Feature is Sketch 
                    if (feature.FeatureType.ToLower().Contains("sketch"))
                    {
                        // Array to Collect the all Entities in Sketch Feature
                        NXObject[] sketchObjects = feature.GetEntities();

                        // Check for sketchObjects array is equal to one
                        // Because No Entity is added to Sketch other than display Entity, 
                        if (sketchObjects.Length == 1)
                        {
                            // Assign Feature Sketch to sketchFeature
                            sketchFeature = feature;
                            break; // exist the loop
                        }
                    }
                }





                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        private static void createLineDimension(Line line)
        {
            NXOpen.Annotations.Dimension nullNXOpen_Annotations_Dimension = null;
            NXOpen.SketchLinearDimensionBuilder sketchLinearDimensionBuilder1;
            sketchLinearDimensionBuilder1 = workPart.Sketches.CreateLinearDimensionBuilder(nullNXOpen_Annotations_Dimension);

            sketchLinearDimensionBuilder1.Origin.Plane.PlaneMethod = NXOpen.Annotations.PlaneBuilder.PlaneMethodType.XyPlane;

            sketchLinearDimensionBuilder1.Origin.SetInferRelativeToGeometry(true);

            NXOpen.Annotations.DimensionUnit dimensionlinearunits1;
            dimensionlinearunits1 = sketchLinearDimensionBuilder1.Style.UnitsStyle.DimensionLinearUnits;

            sketchLinearDimensionBuilder1.Origin.SetInferRelativeToGeometry(true);

            sketchLinearDimensionBuilder1.Origin.SetInferRelativeToGeometry(true);

            NXOpen.Direction nullNXOpen_Direction = null;
            sketchLinearDimensionBuilder1.Measurement.Direction = nullNXOpen_Direction;

            NXOpen.View nullNXOpen_View = null;
            sketchLinearDimensionBuilder1.Measurement.DirectionView = nullNXOpen_View;

            sketchLinearDimensionBuilder1.Style.DimensionStyle.NarrowDisplayType = NXOpen.Annotations.NarrowDisplayOption.None;

            NXOpen.Annotations.DimensionUnit dimensionlinearunits11;
            dimensionlinearunits11 = sketchLinearDimensionBuilder1.Style.UnitsStyle.DimensionLinearUnits;

            NXOpen.Line line1 = line; //(NXOpen.Line)theSession.ActiveSketch.FindObject("Curve Line2");

            Point3d lineStartPoint = line.StartPoint;
            Point3d lineEndPoint = line.EndPoint;

            double x = (lineEndPoint.X - lineStartPoint.X) / 2;
            double y = (lineEndPoint.Y - lineStartPoint.Y) / 2;
            double z = (lineEndPoint.Z - lineStartPoint.Z) / 2;

            NXOpen.Point3d point1 = new NXOpen.Point3d(x, y, z); //(0.7252854846090202, 2.0, 0.0);
            sketchLinearDimensionBuilder1.FirstAssociativity.SetValue(line1, workPart.ModelingViews.WorkView, point1);

            NXOpen.Point3d point1_1 = lineStartPoint; //new NXOpen.Point3d(0.0, 2.0, 0.0);
            NXOpen.Point3d point2_1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            sketchLinearDimensionBuilder1.FirstAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, line1, workPart.ModelingViews.WorkView, point1_1, null, nullNXOpen_View, point2_1);

            NXOpen.Point3d point1_2 = lineEndPoint; //new NXOpen.Point3d(2.0, 2.0, 0.0);
            NXOpen.Point3d point2_2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            sketchLinearDimensionBuilder1.SecondAssociativity.SetValue(NXOpen.InferSnapType.SnapType.End, line1, workPart.ModelingViews.WorkView, point1_2, null, nullNXOpen_View, point2_2);


            NXOpen.Annotations.DimensionUnit dimensionlinearunits26;
            dimensionlinearunits26 = sketchLinearDimensionBuilder1.Style.UnitsStyle.DimensionLinearUnits;

            //sketchLinearDimensionBuilder1.Driving.ExpressionName = "p0";

            sketchLinearDimensionBuilder1.Origin.SetInferRelativeToGeometry(true);

            NXOpen.Annotations.DimensionUnit dimensionlinearunits27;
            dimensionlinearunits27 = sketchLinearDimensionBuilder1.Style.UnitsStyle.DimensionLinearUnits;

            sketchLinearDimensionBuilder1.Origin.SetInferRelativeToGeometryFromLeader(true);
            
            NXOpen.NXObject nXObject1;
            nXObject1 = sketchLinearDimensionBuilder1.Commit();

        }
        private static bool AddCutGeomentry(Sketch sketch)
        {
            try
            {
                // Active Sketch created sketch.
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                double guide1_Length = breadth * 0.28378;
                double guide2_Length = breadth * 0.41891;

                double xValue = length / 2;
                double yValue = guide1_Length/2;
                double zValue = height;

                Point3d point1 = new Point3d(-xValue, -yValue, zValue);
                Point3d point2 = new Point3d(-xValue, yValue, zValue);

                Line line1 = workPart.Curves.CreateLine(point1, point2);

                xValue = length / 2 - wallThickness;
                Point3d point3 = new Point3d(-xValue, -yValue, zValue);
                Point3d point4 = new Point3d(-xValue, yValue, zValue);

                Line line2 = workPart.Curves.CreateLine(point1, point3);
                Line line3 = workPart.Curves.CreateLine(point2, point4);

                yValue = breadth / 2 - wallThickness;
                Point3d point5 = new Point3d(-xValue, -yValue, zValue);
                Point3d point6 = new Point3d(-xValue, yValue, zValue);

                Line line4 = workPart.Curves.CreateLine(point3, point5);
                Line line5 = workPart.Curves.CreateLine(point4, point6);

                Point3d point7 = new Point3d(xValue, -yValue, zValue);
                Point3d point8 = new Point3d(xValue, yValue, zValue);

                Line line6 = workPart.Curves.CreateLine(point5, point7);
                Line line7 = workPart.Curves.CreateLine(point6, point8);

                yValue = guide2_Length / 2;
                Point3d point9 = new Point3d(xValue, -yValue, zValue);
                Point3d point10 = new Point3d(xValue, yValue, zValue);

                Line line8 = workPart.Curves.CreateLine(point7, point9);
                Line line9 = workPart.Curves.CreateLine(point8, point10);

                xValue = length / 2;
                Point3d point11 = new Point3d(xValue, -yValue, zValue);
                Point3d point12 = new Point3d(xValue, yValue, zValue);

                Line line10 = workPart.Curves.CreateLine(point9, point11);
                Line line11 = workPart.Curves.CreateLine(point10, point12);

                Line line12 = workPart.Curves.CreateLine(point11, point12);

                // Add All the lines of Rectangle to Sketch
                sketch.AddGeometry(line1);
                sketch.AddGeometry(line2);
                sketch.AddGeometry(line3);
                sketch.AddGeometry(line4);
                sketch.AddGeometry(line5);
                sketch.AddGeometry(line6);
                sketch.AddGeometry(line7);
                sketch.AddGeometry(line8);
                sketch.AddGeometry(line9);
                sketch.AddGeometry(line10);
                sketch.AddGeometry(line11);
                sketch.AddGeometry(line12);

                // Update the sketch
                theSession.ActiveSketch.Update();

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        private static bool AddSideCutGeomentry(Sketch sketch)
        {
            try
            {
                // Active Sketch created sketch.
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                // Declaration and Assign the point values required to Create Rectangle
                double xValue = length / 2 - (2 * pitchOfPins);
                double yValue = breadth / 2;
                double zValue = 0;

                // Declaration and Assign the Points required for lines to Create Rectangle
                Point3d point1 = new Point3d(-xValue, -yValue, zValue);
                zValue = height - 0.6096;
                Point3d point2 = new Point3d(-xValue, -yValue, zValue);
                xValue = xValue - pitchOfPins;
                Point3d point3 = new Point3d(-xValue, -yValue, zValue);
                zValue = 0;
                Point3d point4 = new Point3d(-xValue, -yValue, zValue);

                // Declaration and Assign the Lines required to Create Rectangle
                Line line1 = workPart.Curves.CreateLine(point1, point2);
                Line line2 = workPart.Curves.CreateLine(point2, point3);
                Line line3 = workPart.Curves.CreateLine(point3, point4);
                Line line4 = workPart.Curves.CreateLine(point4, point1);

                // Add All the lines of Rectangle to Sketch
                sketch.AddGeometry(line1);
                sketch.AddGeometry(line2);
                sketch.AddGeometry(line3);
                sketch.AddGeometry(line4);


                // Update the sketch
                theSession.ActiveSketch.Update();

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);



                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        private static bool AddBaseSeat1Geomentry(Sketch sketch)
        {
            try
            {
                // Active Sketch created sketch.
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                // Declaration and Assign the point values required to Create Rectangle
                double xValue = (length / 2) - (0.889);
                double yValue = (breadth / 2) -(1.8796);
                double zValue = 0;

                Point3d pointR1 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL1 = new Point3d(xValue, -yValue, zValue);
                xValue = xValue - 0.381;
                Point3d pointR2 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL2 = new Point3d(xValue, -yValue, zValue);
                yValue = yValue - 0.5588;
                Point3d pointR3 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL3 = new Point3d(xValue, -yValue, zValue);
                xValue = xValue + 0.381;
                Point3d pointR4 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL4 = new Point3d(xValue, -yValue, zValue);


                Line lineR1 = workPart.Curves.CreateLine(pointR1, pointR2);
                Line lineR2 = workPart.Curves.CreateLine(pointR1, pointR4);
                Line lineR3 = workPart.Curves.CreateLine(pointR3, pointR4);

                Line lineL1 = workPart.Curves.CreateLine(pointL1, pointL2);
                Line lineL2 = workPart.Curves.CreateLine(pointL1, pointL4);
                Line lineL3 = workPart.Curves.CreateLine(pointL3, pointL4);

                Vector3d xVector = new Vector3d(1, 0, 0);
                Vector3d yVector = new Vector3d(0, 1, 0);
                xValue = pointR2.X;
                yValue = pointR2.Y + (0.5588 / 2);
                Point3d centerPoint = new Point3d(xValue, yValue, zValue);
                Arc circleR = workPart.Curves.CreateArc(centerPoint, yVector, xVector, (0.5588 / 2), 0, Math.PI);

                xValue = pointL2.X;
                yValue = pointL2.Y + (0.5588 / 2);
                centerPoint = new Point3d(xValue, yValue, zValue);
                Arc circleL = workPart.Curves.CreateArc(centerPoint, yVector, xVector, (0.5588 / 2), Math.PI, 2 * Math.PI);


                sketch.AddGeometry(lineR1);
                sketch.AddGeometry(lineR2);
                sketch.AddGeometry(lineR3);
                sketch.AddGeometry(circleR);

                sketch.AddGeometry(lineL1);
                sketch.AddGeometry(lineL2);
                sketch.AddGeometry(lineL3);
                sketch.AddGeometry(circleL);
                // Update the sketch
                theSession.ActiveSketch.Update();

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);


                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        private static bool AddBaseCircle1Geomentry(Sketch sketch)
        {
            try
            {
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                // Declaration and Assign the point values required to Create circle
                double xValue = (length / 2) - (2 * pitchOfPins);
                double yValue = (breadth / 2) - wallThickness - VerticalPitch;
                Point3d pinCenterPoint = new Point3d(-xValue, -yValue, 0);

                // Declaration and Assign the circleArc required to Create circle
                Vector3d xVector = new Vector3d(1, 0, 0);
                Vector3d yVector = new Vector3d(0, 1, 0);
                Arc circle = workPart.Curves.CreateArc(pinCenterPoint, xVector, yVector, pinDiameter / 2, 0, 2 * Math.PI);


                // Add All the lines of Rectangle to Sketch
                sketch.AddGeometry(circle);

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        private static bool AddBaseSeat2Geomentry(Sketch sketch)
        {
            try
            {
                // Active Sketch created sketch.
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                // Declaration and Assign the point values required to Create Rectangle
                double xValue = (length / 2) - (1.6256);
                double yValue = (breadth / 2) - (1.143);
                double zValue = 0;

                Point3d pointR1 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL1 = new Point3d(-xValue, yValue, zValue);
                xValue = xValue - 0.5588;
                Point3d pointR2 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL2 = new Point3d(-xValue, yValue, zValue);
                yValue = yValue - 0.381;
                Point3d pointR3 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL3 = new Point3d(-xValue, yValue, zValue);
                xValue = xValue + 0.5588;
                Point3d pointR4 = new Point3d(-xValue, -yValue, zValue);
                Point3d pointL4 = new Point3d(-xValue, yValue, zValue);


                Line lineR1 = workPart.Curves.CreateLine(pointR1, pointR2);
                Line lineR2 = workPart.Curves.CreateLine(pointR1, pointR4);
                Line lineR3 = workPart.Curves.CreateLine(pointR2, pointR3);

                Line lineL1 = workPart.Curves.CreateLine(pointL1, pointL2);
                Line lineL2 = workPart.Curves.CreateLine(pointL1, pointL4);
                Line lineL3 = workPart.Curves.CreateLine(pointL2, pointL3);

                Vector3d xVector = new Vector3d(1, 0, 0);
                Vector3d yVector = new Vector3d(0, 1, 0);
                xValue = pointR2.X - (0.5588 / 2);
                yValue = pointR3.Y;
                Point3d centerPoint = new Point3d(xValue, yValue, zValue);
                Arc circleR = workPart.Curves.CreateArc(centerPoint, xVector, yVector, (0.5588 / 2), 0, Math.PI);

                xValue = pointL2.X - (0.5588 / 2);
                yValue = pointL3.Y;
                centerPoint = new Point3d(xValue, yValue, zValue);
                Arc circleL = workPart.Curves.CreateArc(centerPoint, xVector, yVector, (0.5588 / 2), Math.PI, 2 * Math.PI);


                sketch.AddGeometry(lineR1);
                sketch.AddGeometry(lineR2);
                sketch.AddGeometry(lineR3);
                sketch.AddGeometry(circleR);

                sketch.AddGeometry(lineL1);
                sketch.AddGeometry(lineL2);
                sketch.AddGeometry(lineL3);
                sketch.AddGeometry(circleL);

                // Update the sketch
                theSession.ActiveSketch.Update();

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);


                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        private static bool AddBaseCircle2Geomentry(Sketch sketch)
        {
            try
            {
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                // Declaration and Assign the point values required to Create circle
                double xValue = (length / 2) - wallThickness - hortizontalPitch;
                double yValue = (breadth / 2) - 2.794;
                Point3d pinCenterPoint = new Point3d(-xValue, -yValue, 0);

                // Declaration and Assign the circleArc required to Create circle
                Vector3d xVector = new Vector3d(1, 0, 0);
                Vector3d yVector = new Vector3d(0, 1, 0);
                Arc circle = workPart.Curves.CreateArc(pinCenterPoint, xVector, yVector, pinDiameter / 2, 0, 2 * Math.PI);


                // Add All the lines of Rectangle to Sketch
                sketch.AddGeometry(circle);

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// To Create Pin Sketch Geomentry
        /// </summary>
        /// <param name="sketch">Sketch</param>
        /// <param name="sketchFeature">Out as Sketch Feature</param>
        /// <returns></returns>
        private static bool AddPinGeomentry(Sketch sketch)
        {
            try
            {
                // Active Sketch created sketch.
                theSession.ActiveSketch.Activate(Sketch.ViewReorient.True);

                // Declaration and Assign the point values required to Create circle
                double xValue = (length / 2)- wallThickness - hortizontalPitch ;
                double yValue = (breadth / 2) - wallThickness - VerticalPitch ;
                Point3d pinCenterPoint = new Point3d(-xValue, -yValue, 0);

                // Declaration and Assign the circleArc required to Create circle
                Vector3d xVector = new Vector3d(1, 0, 0);
                Vector3d yVector = new Vector3d(0, 1, 0);
                Arc circle = workPart.Curves.CreateArc(pinCenterPoint, xVector, yVector, pinDiameter / 2, 0, 2 * Math.PI);

                // Add All the lines of Rectangle to Sketch
                sketch.AddGeometry(circle);

                // Update the sketch
                theSession.ActiveSketch.Update();

                // Deactivate the sketch
                sketch.Deactivate(Sketch.ViewReorient.False, Sketch.UpdateLevel.Model);

                //foreach (NXOpen.Features.Feature feature in workPart.Features)
                //{
                //    if (feature.FeatureType.ToLower().Contains("sketch"))
                //    {
                //        sketchFeature = feature;
                //    }
                //}

                //workPart.ModelingViews.WorkView.Fit(); // Fit to Screen

                return true;
            }
            catch (Exception ex)
            {

                throw;
                return false;
            }
        }

        /// <summary>
        /// Extrude the Sketch to given Length
        /// </summary>
        /// <param name="sketch">Sketch to Extrude</param>
        /// <param name="startLength">Start Length of Extrude</param>
        /// <param name="endLength">End Length of Extrude</param>
        /// <param name="extrudeFeature">Out as Feature of Extrude</param>
        /// <returns></returns>
        public static bool SketchExtrude(Sketch sketch, double startLength, double endLength, bool filpDirection, out NXOpen.Features.Feature extrudeFeature)
        {
            try
            {
                // Declarartion of nullNXOpen_Features_Feature
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                // Declarartion of extrudeBuilder1 object Class
                NXOpen.Features.ExtrudeBuilder extrudeBuilder1;
                extrudeBuilder1 = workPart.Features.CreateExtrudeBuilder(nullNXOpen_Features_Feature);

                // Declaration of section1
                NXOpen.Section section1;
                section1 = workPart.Sections.CreateSection(0.009, 0.01, 0.5);

                // Assign section1 to extrudeBuilder1
                extrudeBuilder1.Section = section1;

                extrudeBuilder1.AllowSelfIntersectingSection(true);

                // Assign DistanceTolerance to extrudeBuilder1
                extrudeBuilder1.DistanceTolerance = 0.01;

                // Assign type of Boolean Opearation tpye to extrudeBuilder1
                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Create;

                // Declaration of targetBodies1 Array 
                NXOpen.Body[] targetBodies1 = new NXOpen.Body[1];
                NXOpen.Body nullNXOpen_Body = null;
                targetBodies1[0] = nullNXOpen_Body;
                extrudeBuilder1.BooleanOperation.SetTargetBodies(targetBodies1);

                // Assign the Start Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.StartExtend.Value.RightHandSide = startLength.ToString();

                // Assign the End Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.EndExtend.Value.RightHandSide = endLength.ToString();

                NXOpen.GeometricUtilities.SmartVolumeProfileBuilder smartVolumeProfileBuilder1;
                smartVolumeProfileBuilder1 = extrudeBuilder1.SmartVolumeProfile;

                smartVolumeProfileBuilder1.OpenProfileSmartVolumeOption = false;

                smartVolumeProfileBuilder1.CloseProfileRule = NXOpen.GeometricUtilities.SmartVolumeProfileBuilder.CloseProfileRuleType.Fci;

                section1.DistanceTolerance = 0.01;

                section1.ChainingTolerance = 0.0094999999999999998;

                section1.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves);

                // Declaration of sketchCurvesCount to count the No of Curves in Sketchs
                int sketchCurvesCount = 0;
                // Declaration of sketchCurves List
                List<Curve> sketchCurves = new List<Curve>();
                // Looping through  the All Curves in the Skecth
                foreach (NXObject curves in sketch.GetAllGeometry())
                {
                    sketchCurvesCount++; // Increment the sketchCurvesCount

                    Curve curveInSkecth = (Curve)curves;
                    sketchCurves.Add((Curve)curves); // Add Curves to sketchCurves List

                }

                // Declaration of curves1 and Assign sketchCurves to it
                NXOpen.ICurve[] curves1 = sketchCurves.ToArray(); //new NXOpen.ICurve[sketchCurvesCount];

                // Try to collect the point from the Sketch lines for seed point try this
                NXOpen.Point3d seedPoint1 = new NXOpen.Point3d(1.9, 2.1, 0.0);
                NXOpen.RegionBoundaryRule regionBoundaryRule1;
                regionBoundaryRule1 = workPart.ScRuleFactory.CreateRuleRegionBoundary(sketch, curves1, seedPoint1, 0.01);

                section1.AllowSelfIntersection(true);

                // Declaration of rules1
                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = regionBoundaryRule1;
                NXOpen.NXObject nullNXOpen_NXObject = null;
                NXOpen.Point3d helpPoint1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                section1.AddToSection(rules1, nullNXOpen_NXObject, nullNXOpen_NXObject, nullNXOpen_NXObject, helpPoint1, NXOpen.Section.Mode.Create, false);

                // Declation of direction1
                NXOpen.Direction direction1;
                if(filpDirection)
                    direction1 = workPart.Directions.CreateDirection(sketch, NXOpen.Sense.Reverse, NXOpen.SmartObject.UpdateOption.WithinModeling);
                else
                    direction1 = workPart.Directions.CreateDirection(sketch, NXOpen.Sense.Forward, NXOpen.SmartObject.UpdateOption.WithinModeling);
                
                // Assign direction1 to extrudeBuilder1
                extrudeBuilder1.Direction = direction1;

                extrudeBuilder1.ParentFeatureInternal = false;

                //NXOpen.Features.Feature feature1;
                //feature1 = extrudeBuilder1.CommitFeature();
                // Commit the extrudeBuilder1
                extrudeFeature = extrudeBuilder1.CommitFeature();

                // Destroy the extrudeBuilder1
                extrudeBuilder1.Destroy();

                return true;
            }
            catch (Exception)
            {

                throw;
                return false;
            }
        }
        /// <summary>
        /// Extrude the Sketch Feature to given Length
        /// </summary>
        /// <param name="sketchFeat">Sketch Feature to Extrude</param>
        /// <param name="sketch">Sketch to Extrude</param>
        /// <param name="startLength">Start Length of Extrude</param>
        /// <param name="endLength">End Length of Extrude</param>
        /// <param name="filpDirection">To Change the Extrude Direction</param>
        /// <param name="extrudeFeature">Out as Feature of Extrude</param>
        /// <returns></returns>
        public static bool SketchExtrude(NXOpen.Features.Feature sketchFeat, NXOpen.Sketch sketch, double startLength, double endLength, bool filpDirection, out NXOpen.Features.Feature extrudeFeature)
        {
            try
            {
                // Declarartion of nullNXOpen_Features_Feature
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                // Declarartion of extrudeBuilder1 object Class
                NXOpen.Features.ExtrudeBuilder extrudeBuilder1;
                extrudeBuilder1 = workPart.Features.CreateExtrudeBuilder(nullNXOpen_Features_Feature);

                // Declaration of section1
                NXOpen.Section section1;
                section1 = workPart.Sections.CreateSection(0.009, 0.01, 0.5);

                // Assign section1 to extrudeBuilder1
                extrudeBuilder1.Section = section1;

                extrudeBuilder1.AllowSelfIntersectingSection(true);

                // Assign DistanceTolerance to extrudeBuilder1
                extrudeBuilder1.DistanceTolerance = 0.01;

                // Assign type of Boolean Opearation tpye to extrudeBuilder1
                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Create;

                NXOpen.GeometricUtilities.SmartVolumeProfileBuilder smartVolumeProfileBuilder1;
                smartVolumeProfileBuilder1 = extrudeBuilder1.SmartVolumeProfile;

                smartVolumeProfileBuilder1.OpenProfileSmartVolumeOption = false;

                smartVolumeProfileBuilder1.CloseProfileRule = NXOpen.GeometricUtilities.SmartVolumeProfileBuilder.CloseProfileRuleType.Fci;

                section1.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves);

                NXOpen.Features.Feature[] features1 = new NXOpen.Features.Feature[1];
                features1[0] = sketchFeat;
                NXOpen.CurveFeatureRule curveFeatureRule1;
                curveFeatureRule1 = workPart.ScRuleFactory.CreateRuleCurveFeature(features1);

                NXOpen.Features.SketchFeature sketchFeature1 = (NXOpen.Features.SketchFeature)sketchFeat;
                section1.AllowSelfIntersection(true);

                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = curveFeatureRule1;
                NXOpen.Sketch sketch1 = sketch;

                NXObject[] lines = sketchFeat.GetEntities();
                Line lineInSketch = null;
                foreach (NXObject nXObject in lines)
                {
                    try
                    {
                        lineInSketch = (NXOpen.Line)nXObject;
                    }
                    catch (Exception ex)
                    {

                    }
                }
                NXOpen.Line line1 = lineInSketch;
                NXOpen.NXObject nullNXOpen_NXObject = null;
                NXOpen.Point3d helpPoint1 = new NXOpen.Point3d(0, 0, 0);
                section1.AddToSection(rules1, line1, nullNXOpen_NXObject, nullNXOpen_NXObject, helpPoint1, NXOpen.Section.Mode.Create, false);

                // Declation of direction1
                NXOpen.Direction direction1;
                if (filpDirection)
                    direction1 = workPart.Directions.CreateDirection(sketch, NXOpen.Sense.Reverse, NXOpen.SmartObject.UpdateOption.WithinModeling);
                else
                    direction1 = workPart.Directions.CreateDirection(sketch, NXOpen.Sense.Forward, NXOpen.SmartObject.UpdateOption.WithinModeling);

                // Assign direction1 to extrudeBuilder1
                extrudeBuilder1.Direction = direction1;

                // Assign the Start Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.StartExtend.Value.RightHandSide = startLength.ToString();

                // Assign the End Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.EndExtend.Value.RightHandSide = endLength.ToString();

                extrudeBuilder1.ParentFeatureInternal = false;

                // Commit the extrudeBuilder1
                extrudeFeature = extrudeBuilder1.CommitFeature();

                // Destroy the extrudeBuilder1
                extrudeBuilder1.Destroy();

                return true;

            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }
        /// <summary>
        /// Extrude the Sketch Feature to given Length
        /// </summary>
        /// <param name="baseBody">On which Body to Unite Or Subtract</param>
        /// <param name="sketchFeat">Sketch Feature to Extrude</param>
        /// <param name="sketch">Sketch to Extrude</param>
        /// <param name="startLength">Start Length of Extrude</param>
        /// <param name="endLength">End Length of Extrude</param>
        /// <param name="unite">True for Unite and False for Subtract from given Body</param>
        /// <param name="flipDirection">True for Change the Extrude Direction ans False for not to change Direction</param>
        /// <param name="extrudeFeature">Out as Feature of Extrude</param>
        /// <returns></returns>
        public static bool SketchExtrudeBoolean(Body baseBody, NXOpen.Features.Feature sketchFeat, NXOpen.Sketch sketch, double startLength, double endLength,bool unite, bool flipDirection, out NXOpen.Features.Feature extrudeFeature)
        {
            try
            {
                // Declarartion of nullNXOpen_Features_Feature
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                // Declarartion of extrudeBuilder1 object Class
                NXOpen.Features.ExtrudeBuilder extrudeBuilder1;
                extrudeBuilder1 = workPart.Features.CreateExtrudeBuilder(nullNXOpen_Features_Feature);

                // Declaration of section1
                NXOpen.Section section1;
                section1 = workPart.Sections.CreateSection(0.009, 0.01, 0.5);

                // Assign section1 to extrudeBuilder1
                extrudeBuilder1.Section = section1;

                extrudeBuilder1.AllowSelfIntersectingSection(true);

                // Assign DistanceTolerance to extrudeBuilder1
                extrudeBuilder1.DistanceTolerance = 0.01;

                // Assign type of Boolean Opearation tpye to extrudeBuilder1
                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Create;


                NXOpen.Body[] targetBodies1 = { baseBody }; //workPart.Bodies.ToArray(); //new NXOpen.Body[1];
                //NXOpen.Body nullNXOpen_Body = null;
                //targetBodies1[0] = nullNXOpen_Body;
                extrudeBuilder1.BooleanOperation.SetTargetBodies(targetBodies1);

                NXOpen.GeometricUtilities.SmartVolumeProfileBuilder smartVolumeProfileBuilder1;
                smartVolumeProfileBuilder1 = extrudeBuilder1.SmartVolumeProfile;

                smartVolumeProfileBuilder1.OpenProfileSmartVolumeOption = false;

                smartVolumeProfileBuilder1.CloseProfileRule = NXOpen.GeometricUtilities.SmartVolumeProfileBuilder.CloseProfileRuleType.Fci;

                section1.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves);

                NXOpen.Features.Feature[] features1 = new NXOpen.Features.Feature[1];
                features1[0] = sketchFeat;
                NXOpen.CurveFeatureRule curveFeatureRule1;
                curveFeatureRule1 = workPart.ScRuleFactory.CreateRuleCurveFeature(features1);

                NXOpen.Features.SketchFeature sketchFeature1 = (NXOpen.Features.SketchFeature)sketchFeat;
                section1.AllowSelfIntersection(true);

                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = curveFeatureRule1;
                NXOpen.Sketch sketch1 = sketch;

                NXObject[] lines = sketchFeat.GetEntities();
                Line lineInSketch = null;
                foreach (NXObject nXObject in lines)
                {
                    try
                    {
                        lineInSketch = (NXOpen.Line)nXObject;
                    }
                    catch (Exception ex)
                    {

                    }
                }
                NXOpen.Line line1 = lineInSketch;
                NXOpen.NXObject nullNXOpen_NXObject = null;
                NXOpen.Point3d helpPoint1 = new NXOpen.Point3d(0, 0, 0);
                section1.AddToSection(rules1, line1, nullNXOpen_NXObject, nullNXOpen_NXObject, helpPoint1, NXOpen.Section.Mode.Create, false);

                // Declation of direction1
                NXOpen.Direction direction1;
                if (flipDirection)                
                    direction1 = workPart.Directions.CreateDirection(sketch1, NXOpen.Sense.Reverse, NXOpen.SmartObject.UpdateOption.WithinModeling);
                else                
                    direction1 = workPart.Directions.CreateDirection(sketch1, NXOpen.Sense.Forward, NXOpen.SmartObject.UpdateOption.WithinModeling);
               
                

                // Assign direction1 to extrudeBuilder1
                extrudeBuilder1.Direction = direction1;

                if(unite)
                    extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Unite;
                else
                    extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Subtract;
                
                // Assign the Start Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.StartExtend.Value.RightHandSide = startLength.ToString();

                // Assign the End Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.EndExtend.Value.RightHandSide = endLength.ToString();

                extrudeBuilder1.ParentFeatureInternal = false;

                // Commit the extrudeBuilder1
                extrudeFeature = extrudeBuilder1.CommitFeature();

                // Destroy the extrudeBuilder1
                extrudeBuilder1.Destroy();

                return true;

            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        public static bool SketchExtrudeCut(Body baseBody, NXOpen.Features.Feature sketchFeat, NXOpen.Sketch sketch, double startLength, double endLength, out NXOpen.Features.Feature extrudeFeature)
        {
            try
            {
                // Declarartion of nullNXOpen_Features_Feature
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                // Declarartion of extrudeBuilder1 object Class
                NXOpen.Features.ExtrudeBuilder extrudeBuilder1;
                extrudeBuilder1 = workPart.Features.CreateExtrudeBuilder(nullNXOpen_Features_Feature);

                // Declaration of section1
                NXOpen.Section section1;
                section1 = workPart.Sections.CreateSection(0.009, 0.01, 0.5);

                // Assign section1 to extrudeBuilder1
                extrudeBuilder1.Section = section1;

                extrudeBuilder1.AllowSelfIntersectingSection(true);

                // Assign DistanceTolerance to extrudeBuilder1
                extrudeBuilder1.DistanceTolerance = 0.01;

                // Assign type of Boolean Opearation tpye to extrudeBuilder1
                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Create;


                NXOpen.Body[] targetBodies1 = { baseBody }; //workPart.Bodies.ToArray(); //new NXOpen.Body[1];
                //NXOpen.Body nullNXOpen_Body = null;
                //targetBodies1[0] = nullNXOpen_Body;
                extrudeBuilder1.BooleanOperation.SetTargetBodies(targetBodies1);

                NXOpen.GeometricUtilities.SmartVolumeProfileBuilder smartVolumeProfileBuilder1;
                smartVolumeProfileBuilder1 = extrudeBuilder1.SmartVolumeProfile;

                smartVolumeProfileBuilder1.OpenProfileSmartVolumeOption = false;

                smartVolumeProfileBuilder1.CloseProfileRule = NXOpen.GeometricUtilities.SmartVolumeProfileBuilder.CloseProfileRuleType.Fci;

                section1.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves);

                NXOpen.Features.Feature[] features1 = new NXOpen.Features.Feature[1];
                features1[0] = sketchFeat;
                NXOpen.CurveFeatureRule curveFeatureRule1;
                curveFeatureRule1 = workPart.ScRuleFactory.CreateRuleCurveFeature(features1);

                NXOpen.Features.SketchFeature sketchFeature1 = (NXOpen.Features.SketchFeature)sketchFeat;
                section1.AllowSelfIntersection(true);

                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = curveFeatureRule1;
                NXOpen.Sketch sketch1 = sketch;

                NXObject[] lines = sketchFeat.GetEntities();
                Line lineInSketch = null;
                foreach (NXObject nXObject in lines)
                {
                    try
                    {
                        lineInSketch = (NXOpen.Line)nXObject;
                    }
                    catch (Exception ex)
                    {

                    }
                }
                NXOpen.Line line1 = lineInSketch;
                NXOpen.NXObject nullNXOpen_NXObject = null;
                NXOpen.Point3d helpPoint1 = new NXOpen.Point3d(0, 0, 0);
                section1.AddToSection(rules1, line1, nullNXOpen_NXObject, nullNXOpen_NXObject, helpPoint1, NXOpen.Section.Mode.Create, false);

                // Declation of direction1
                NXOpen.Direction direction1;
                direction1 = workPart.Directions.CreateDirection(sketch1, NXOpen.Sense.Reverse, NXOpen.SmartObject.UpdateOption.WithinModeling);

                // Assign direction1 to extrudeBuilder1
                extrudeBuilder1.Direction = direction1;
                               
                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Subtract;

                // Assign the Start Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.StartExtend.Value.RightHandSide = startLength.ToString();

                // Assign the End Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.EndExtend.Value.RightHandSide = endLength.ToString();

                extrudeBuilder1.ParentFeatureInternal = false;

                // Commit the extrudeBuilder1
                extrudeFeature = extrudeBuilder1.CommitFeature();

                // Destroy the extrudeBuilder1
                extrudeBuilder1.Destroy();

                return true;

            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }
        
        public static bool SketchExtrudeAdd(Body baseBody, NXOpen.Features.Feature sketchFeat, NXOpen.Sketch sketch, double startLength, double endLength, out NXOpen.Features.Feature extrudeFeature)
        {
            try
            {
                // Declarartion of nullNXOpen_Features_Feature
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                // Declarartion of extrudeBuilder1 object Class
                NXOpen.Features.ExtrudeBuilder extrudeBuilder1;
                extrudeBuilder1 = workPart.Features.CreateExtrudeBuilder(nullNXOpen_Features_Feature);

                // Declaration of section1
                NXOpen.Section section1;
                section1 = workPart.Sections.CreateSection(0.009, 0.01, 0.5);

                // Assign section1 to extrudeBuilder1
                extrudeBuilder1.Section = section1;

                extrudeBuilder1.AllowSelfIntersectingSection(true);

                // Assign DistanceTolerance to extrudeBuilder1
                extrudeBuilder1.DistanceTolerance = 0.01;

                // Assign type of Boolean Opearation tpye to extrudeBuilder1
                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Create;


                NXOpen.Body[] targetBodies1 = { baseBody }; //workPart.Bodies.ToArray(); //new NXOpen.Body[1];
                //NXOpen.Body nullNXOpen_Body = null;
                //targetBodies1[0] = nullNXOpen_Body;
                extrudeBuilder1.BooleanOperation.SetTargetBodies(targetBodies1);

                NXOpen.GeometricUtilities.SmartVolumeProfileBuilder smartVolumeProfileBuilder1;
                smartVolumeProfileBuilder1 = extrudeBuilder1.SmartVolumeProfile;

                smartVolumeProfileBuilder1.OpenProfileSmartVolumeOption = false;

                smartVolumeProfileBuilder1.CloseProfileRule = NXOpen.GeometricUtilities.SmartVolumeProfileBuilder.CloseProfileRuleType.Fci;

                section1.SetAllowedEntityTypes(NXOpen.Section.AllowTypes.OnlyCurves);

                NXOpen.Features.Feature[] features1 = new NXOpen.Features.Feature[1];
                features1[0] = sketchFeat;
                NXOpen.CurveFeatureRule curveFeatureRule1;
                curveFeatureRule1 = workPart.ScRuleFactory.CreateRuleCurveFeature(features1);

                NXOpen.Features.SketchFeature sketchFeature1 = (NXOpen.Features.SketchFeature)sketchFeat;
                section1.AllowSelfIntersection(true);

                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = curveFeatureRule1;
                NXOpen.Sketch sketch1 = sketch;

                NXObject[] lines = sketchFeat.GetEntities();
                Line lineInSketch = null;
                foreach (NXObject nXObject in lines)
                {
                    try
                    {
                        lineInSketch = (NXOpen.Line)nXObject;
                    }
                    catch (Exception ex)
                    {

                    }
                }
                NXOpen.Line line1 = lineInSketch;
                NXOpen.NXObject nullNXOpen_NXObject = null;
                NXOpen.Point3d helpPoint1 = new NXOpen.Point3d(0, 0, 0);
                section1.AddToSection(rules1, line1, nullNXOpen_NXObject, nullNXOpen_NXObject, helpPoint1, NXOpen.Section.Mode.Create, false);

                // Declation of direction1
                NXOpen.Direction direction1;
                direction1 = workPart.Directions.CreateDirection(sketch1, NXOpen.Sense.Reverse, NXOpen.SmartObject.UpdateOption.WithinModeling);

                // Assign direction1 to extrudeBuilder1
                extrudeBuilder1.Direction = direction1;

                extrudeBuilder1.BooleanOperation.Type = NXOpen.GeometricUtilities.BooleanOperation.BooleanType.Unite;

                // Assign the Start Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.StartExtend.Value.RightHandSide = startLength.ToString();

                // Assign the End Value of the Extrude to extrudeBuilder1
                extrudeBuilder1.Limits.EndExtend.Value.RightHandSide = endLength.ToString();

                extrudeBuilder1.ParentFeatureInternal = false;

                // Commit the extrudeBuilder1
                extrudeFeature = extrudeBuilder1.CommitFeature();

                // Destroy the extrudeBuilder1
                extrudeBuilder1.Destroy();

                return true;

            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// To Change the Color of the Body 
        /// </summary>
        /// <param name="body">Pass the Body to Change the Color</param>
        /// <param name="colorCode">Required Color Code</param>
        /// <returns></returns>
        private static bool ChangeColor(Body body, int colorCode)
        {
            try
            {
                // Declarartion assign of displayModification1 
                NXOpen.DisplayModification displayModification1 = null;
                displayModification1 = theSession.DisplayManager.NewDisplayModification();

                displayModification1.ApplyToAllFaces = true;

                displayModification1.ApplyToOwningParts = false;

                // Assign the Color Code to displayModification1
                displayModification1.NewColor = colorCode;

                /*
                 * Black color = 216
                 * Grey = 130
                 * Iron Gray = 201
                 * Yellow color = 6
                 * Strong Lemon = 4
                 * Strong Azure = 141
                 * Red = 186
                 * Green = 36
                 * Magenta (Pink) = 181
                 * Blue = 211                 * 
                 * 
                 */

                // Assign Body to objects1 array to be Colored
                NXOpen.DisplayableObject[] objects1 = { body };
                displayModification1.Apply(objects1);

                // Dispose the displayModification1 
                displayModification1.Dispose();

                return true;
            }
            catch (Exception ex)
            {

                throw;
                return false;
            }
        }
       
        /// <summary>
        /// To Change the Color of the given Faces
        /// </summary>
        /// <param name="faces">Array Faces to change the Color</param>
        /// <param name="colorCode">Required Color Code </param>
        /// <returns></returns>
        private static bool ChangeColor(Face[] faces, int colorCode)
        {
            try
            {
                // Declarartion assign of displayModification1 
                NXOpen.DisplayModification displayModification1 = null;
                displayModification1 = theSession.DisplayManager.NewDisplayModification();

                displayModification1.ApplyToAllFaces = true;

                displayModification1.ApplyToOwningParts = false;

                // Assign the Color Code to displayModification1
                displayModification1.NewColor = colorCode;

                /*
                 * Black color = 216
                 * Grey = 130
                 * Iron Gray = 201
                 * Yellow color = 6
                 * Strong Lemon = 4
                 * Strong Azure = 141
                 * Red = 186
                 * Green = 36
                 * Magenta (Pink) = 181
                 * Blue = 211                 * 
                 * 
                 */
                // Assign Faces Array to objects1 array to be Colored
                NXOpen.DisplayableObject[] objects1 = faces;
                displayModification1.Apply(objects1);

                // Dispose the displayModification1 
                displayModification1.Dispose();

                return true;
            }
            catch (Exception ex)
            {

                throw;
                return false;
            }
        }

        /// <summary>
        /// Gives Common Edges from given Face List
        /// </summary>
        /// <param name="facesList">Faces to find the Common Edges amoung them</param>
        /// <param name="edgesList">Out the Common Edges of Given Face List</param>        
        /// <returns></returns>
        private static bool CommonEdgesOfFaces(List<Face> facesList, out List<Edge> edgesList)
        {
            // Declaration of the edgesList
            edgesList = new List<Edge>();
            try
            {
                // Looping throughg the Faces in facesList
                foreach (Face face1 in facesList)
                {
                    // Create a copy of faceslist with current iteration face in filterFace List
                    List<Face> filterFace = facesList.Where(removeFace => removeFace != face1).ToList();
                    // Lopping through the Faces in filterFace List
                    foreach (Face face2 in filterFace)
                    {
                        // Looping Through the Edges of the face1
                        foreach (Edge face1Egde in face1.GetEdges())
                        {
                            // Looping Through the Edges of the face2
                            foreach (Edge face2Egde in face2.GetEdges())
                            {
                                // Check for common Edge of the face1 and face2
                                if (face2Egde == face1Egde)
                                {
                                    // check for Edge is already exists in edgesList
                                    if (!edgesList.Contains(face1Egde))
                                    {
                                        edgesList.Add(face1Egde); // Add Edge to edgesList                                     
                                        //face1Egde.Highlight();
                                    }
                                }
                            }
                        }
                    }
                    // Clear the filterFace
                    filterFace.Clear();
                }
                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }
        /// <summary>
        /// Gives Common Edges from given Face Lists
        /// </summary>
        /// <param name="facesList1">Faces to find the Common Edges </param>
        /// <param name="facesList2">Faces to find the Common Edges </param>
        /// <param name="edgesList">Out the Common Edges of Given Face List</param>        
        /// <returns></returns>
        private static bool CommonEdgesOfFaces(List<Face> facesList1,List<Face> facesList2, out List<Edge> edgesList)
        {
            // Declaration of the edgesList
            edgesList = new List<Edge>();
            try
            {
                // Looping throughg the Faces in facesList1
                foreach (Face face1 in facesList1)
                {
                    // Get the all Edge of face of  facesList1
                    List<Edge> faceEdges = face1.GetEdges().ToList();

                    // Lopping through the Faces in facesList2
                    foreach (Face face2 in facesList2)
                    {
                        // Looping Through the Edges of the face2
                        foreach (Edge face2Egde in face2.GetEdges())
                        {
                            // Check for common Edge of the face1 and face2
                            if (faceEdges.Contains(face2Egde))
                            {
                                // check for Edge is already exists in edgesList
                                if (!edgesList.Contains(face2Egde))
                                {
                                    edgesList.Add(face2Egde); // Add Edge to edgesList                                     
                                    //face2Egde.Highlight();
                                }
                            }
                        }
                    }
                    //Clear the faceEdges
                    faceEdges.Clear();
                }
                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }

        /// <summary>
        /// To Create a Chamfer 
        /// </summary>
        /// <param name="edges">List of Edges to Create Chamfer</param>
        /// <param name="chamferLength"> Lenght Chamfer</param>
        /// <param name="chamferFeature">Out as Chamfer Feature</param>
        /// <returns></returns>
        private static bool CreateChamfer(List<Edge> edges,double chamferLength , out NXOpen.Features.Feature chamferFeature)
        {
            try
            {
                NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
                NXOpen.Features.ChamferBuilder chamferBuilder1;
                chamferBuilder1 = workPart.Features.CreateChamferBuilder(nullNXOpen_Features_Feature);

                chamferBuilder1.FirstOffsetExp.RightHandSide = chamferLength.ToString();

                chamferBuilder1.SecondOffsetExp.RightHandSide = chamferLength.ToString();

                chamferBuilder1.AngleExp.RightHandSide = "45";

                chamferBuilder1.Option = NXOpen.Features.ChamferBuilder.ChamferOption.OffsetAndAngle;

                chamferBuilder1.Method = NXOpen.Features.ChamferBuilder.OffsetMethod.EdgesAlongFaces;

                chamferBuilder1.Tolerance = 0.01;


                NXOpen.ScCollector scCollector1;
                scCollector1 = workPart.ScCollectors.CreateCollector();

                NXOpen.Edge[] edges1 = edges.ToArray();
                NXOpen.EdgeDumbRule edgeDumbRule1;
                edgeDumbRule1 = workPart.ScRuleFactory.CreateRuleEdgeDumb(edges1);

                NXOpen.SelectionIntentRule[] rules1 = new NXOpen.SelectionIntentRule[1];
                rules1[0] = edgeDumbRule1;
                scCollector1.ReplaceRules(rules1, false);

                chamferBuilder1.SmartCollector = scCollector1;


                //NXOpen.Features.Feature feature1;
                chamferFeature = chamferBuilder1.CommitFeature();

                chamferBuilder1.Destroy();

                return true;
            }
            catch (Exception ex)
            {

                throw;
                return false;
            }
        }

        /// <summary>
        /// To Create Linear Pattern for Feature
        /// </summary>
        /// <param name="feature">Array of Feature to Perform Linear Pattern</param>
        /// <param name="xdirection">First Direction of Pattern</param>
        /// <param name="NoOfCopiesXDirection">No of Copies in First Direction</param>
        /// <param name="pitchDistanceXDirection">Pitch in First Direction</param>
        /// <param name="ydirection">Second Direction of Pattern</param>
        /// <param name="NoOfCopiesYDirection">No of Copies in Second Direction</param>
        /// <param name="pitchDistanceYDirection">Pitch in Second Direction</param>
        private static void LinearPattern(Feature[] feature, Vector3d xdirection, double NoOfCopiesXDirection, double pitchDistanceXDirection, Vector3d ydirection, double NoOfCopiesYDirection=1, double pitchDistanceYDirection=1)
        {
            // Declare and Initialize Feature Class Object
            NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
            // Declare PatternFeatureBuilder class Object
            NXOpen.Features.PatternFeatureBuilder patternFeatureBuilder1;
            patternFeatureBuilder1 = workPart.Features.CreatePatternFeatureBuilder(nullNXOpen_Features_Feature);


            bool added1;// Declaration of Boolean to Add Features Array
            added1 = patternFeatureBuilder1.FeatureList.Add(feature);

            // Origin Point of the Palne
            NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            // Declaration of Normal Vector for Pattern Plane
            NXOpen.Vector3d normal1 = new NXOpen.Vector3d();

            // Check to create Normal vector Base on the Given XDirection and Y Direction
            if (xdirection.X == 1 && ydirection.Y == 1)
            {
                // Normal in Z Direction
                normal1.X = 0;
                normal1.Y = 0;
                normal1.Z = 1;
            }
            else if (xdirection.Y == 1 && ydirection.Z == 1)
            {
                // Normal in X Direction
                normal1.X = 1;
                normal1.Y = 0;
                normal1.Z = 0;
            }
            else
            {
                // Normal in Y Direction
                normal1.X = 0;
                normal1.Y = 1;
                normal1.Z = 0;
            }

            // Declaration of the plane and create Pattern Plane
            NXOpen.Plane plane1;
            plane1 = workPart.Planes.CreatePlane(origin1, normal1, NXOpen.SmartObject.UpdateOption.WithinModeling);

            // Assign Plane to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.MirrorDefinition.NewPlane = plane1;

            // Origin Point for the First Direction 
            NXOpen.Point3d origin2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            // Vector for the X Direction
            NXOpen.Vector3d vector1 = xdirection; //new NXOpen.Vector3d(1.0, 0.0, 0.0);
            // Declaration and Assign of first direction for Pattern
            NXOpen.Direction direction1;
            direction1 = workPart.Directions.CreateDirection(origin2, vector1, NXOpen.SmartObject.UpdateOption.WithinModeling);

            // Assign first direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.XDirection = direction1;

            // Origin Point for the Second Direction 
            NXOpen.Point3d origin3 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            // Vector for the X Direction
            NXOpen.Vector3d vector2 = ydirection; //new NXOpen.Vector3d(0.0, 1.0, 0.0);
            // Declaration and Assign of Second direction for Pattern
            NXOpen.Direction direction2;
            direction2 = workPart.Directions.CreateDirection(origin3, vector2, NXOpen.SmartObject.UpdateOption.WithinModeling);

            // Assign Second Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.YDirection = direction2;

            // Assign No of Copies in First Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.XSpacing.NCopies.RightHandSide = NoOfCopiesXDirection.ToString(); //"6";

            // Assign Pitch Distance in First Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.XSpacing.PitchDistance.RightHandSide = pitchDistanceXDirection.ToString();  //"30";

            // Assign Ture for Second Direction for patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.UseYDirectionToggle = true;

            // Assign No of Copies in Second Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.YSpacing.NCopies.RightHandSide = NoOfCopiesYDirection.ToString(); //"3";

            // Assign Pitch Distance in Second Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.YSpacing.PitchDistance.RightHandSide = pitchDistanceYDirection.ToString(); //"30";

            patternFeatureBuilder1.ParentFeatureInternal = false;

            // Feature Object Output
            NXOpen.NXObject nXObject1;
            // Commits the feature parameters and Creates the Feature
            nXObject1 = patternFeatureBuilder1.Commit();
            // Destroy the Builder. It deletes the builder, and the cleans up any objects create by the Builder  
            patternFeatureBuilder1.Destroy();
        }

        /// <summary>
        /// To Create Linear Pattern for Feature
        /// </summary>
        /// <param name="feature">Array of Feature to Perform Linear Pattern</param>
        /// <param name="xdirection">First Direction of Pattern</param>
        /// <param name="NoOfCopiesXDirection">No of Copies in First Direction</param>
        /// <param name="pitchDistanceXDirection">Pitch in First Direction</param>
        /// <param name="ydirection">Second Direction of Pattern</param>
        /// <param name="NoOfCopiesYDirection">No of Copies in Second Direction</param>
        /// <param name="pitchDistanceYDirection">Pitch in Second Direction</param>
        private static void LinearPattern(Feature feature, Vector3d xdirection, double NoOfCopiesXDirection, double pitchDistanceXDirection, Vector3d ydirection, double NoOfCopiesYDirection=1, double pitchDistanceYDirection=1)
        {
            // Declare and Initialize Feature Class Object
            NXOpen.Features.Feature nullNXOpen_Features_Feature = null;
            // Declare PatternFeatureBuilder class Object
            NXOpen.Features.PatternFeatureBuilder patternFeatureBuilder1;
            patternFeatureBuilder1 = workPart.Features.CreatePatternFeatureBuilder(nullNXOpen_Features_Feature);


            bool added1;// Declaration of Boolean to Add Features Array
            added1 = patternFeatureBuilder1.FeatureList.Add(feature);

            // Origin Point of the Palne
            NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            // Declaration of Normal Vector for Pattern Plane
            NXOpen.Vector3d normal1 = new NXOpen.Vector3d();

            // Check to create Normal vector Base on the Given XDirection and Y Direction
            if (xdirection.X == 1 && ydirection.Y == 1)
            {
                // Normal in Z Direction
                normal1.X = 0;
                normal1.Y = 0;
                normal1.Z = 1;
            }
            else if (xdirection.Y == 1 && ydirection.Z == 1)
            {
                // Normal in X Direction
                normal1.X = 1;
                normal1.Y = 0;
                normal1.Z = 0;
            }
            else
            {
                // Normal in Y Direction
                normal1.X = 0;
                normal1.Y = 1;
                normal1.Z = 0;
            }

            // Declaration of the plane and create Pattern Plane
            NXOpen.Plane plane1;
            plane1 = workPart.Planes.CreatePlane(origin1, normal1, NXOpen.SmartObject.UpdateOption.WithinModeling);

            // Assign Plane to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.MirrorDefinition.NewPlane = plane1;

            // Origin Point for the First Direction 
            NXOpen.Point3d origin2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            // Vector for the X Direction
            NXOpen.Vector3d vector1 = xdirection; //new NXOpen.Vector3d(1.0, 0.0, 0.0);
            // Declaration and Assign of first direction for Pattern
            NXOpen.Direction direction1;
            direction1 = workPart.Directions.CreateDirection(origin2, vector1, NXOpen.SmartObject.UpdateOption.WithinModeling);

            // Assign first direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.XDirection = direction1;

            // Origin Point for the Second Direction 
            NXOpen.Point3d origin3 = new NXOpen.Point3d(0.0, 0.0, 0.0);
            // Vector for the X Direction
            NXOpen.Vector3d vector2 = ydirection; //new NXOpen.Vector3d(0.0, 1.0, 0.0);
            // Declaration and Assign of Second direction for Pattern
            NXOpen.Direction direction2;
            direction2 = workPart.Directions.CreateDirection(origin3, vector2, NXOpen.SmartObject.UpdateOption.WithinModeling);

            // Assign Second Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.YDirection = direction2;

            // Assign No of Copies in First Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.XSpacing.NCopies.RightHandSide = NoOfCopiesXDirection.ToString(); //"6";

            // Assign Pitch Distance in First Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.XSpacing.PitchDistance.RightHandSide = pitchDistanceXDirection.ToString();  //"30";

            // Assign Ture for Second Direction for patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.UseYDirectionToggle = true;

            // Assign No of Copies in Second Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.YSpacing.NCopies.RightHandSide = NoOfCopiesYDirection.ToString(); //"3";

            // Assign Pitch Distance in Second Direction to the patternFeatureBuilder1
            patternFeatureBuilder1.PatternService.RectangularDefinition.YSpacing.PitchDistance.RightHandSide = pitchDistanceYDirection.ToString(); //"30";

            patternFeatureBuilder1.ParentFeatureInternal = false;

            // Feature Object Output
            NXOpen.NXObject nXObject1;
            // Commits the feature parameters and Creates the Feature
            nXObject1 = patternFeatureBuilder1.Commit();
            // Destroy the Builder. It deletes the builder, and the cleans up any objects create by the Builder  
            patternFeatureBuilder1.Destroy();
        }

        /// <summary>
        /// To Create Mirror Feature
        /// </summary>
        /// <param name="features">Array of Feature to Perform Mirror</param>
        /// <param name="mirrorPlane">Mirror Plane </param>
        /// <param name="mirrorFeature">Out Mirror Feature</param>
        /// <returns></returns>
        private static bool MirrorFeature(NXOpen.Features.Feature[] features, DatumPlane mirrorPlane, out NXOpen.Features.Feature mirrorFeature)
        {
            try
            {

                NXOpen.Features.Mirror nullNXOpen_Features_Mirror = null;
                NXOpen.Features.MirrorBuilder mirrorBuilder1;
                mirrorBuilder1 = workPart.Features.CreateMirrorBuilder(nullNXOpen_Features_Mirror);

                NXOpen.Point3d origin1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                NXOpen.Vector3d normal1 = new NXOpen.Vector3d(0.0, 0.0, 1.0);
                NXOpen.Plane plane1;
                plane1 = workPart.Planes.CreatePlane(origin1, normal1, NXOpen.SmartObject.UpdateOption.WithinModeling);

                mirrorBuilder1.PatternService.MirrorDefinition.NewPlane = plane1;

                mirrorBuilder1.PatternService.PatternType = NXOpen.GeometricUtilities.PatternDefinition.PatternEnum.Mirror;

                mirrorBuilder1.CsysMirrorOption = NXOpen.Features.MirrorBuilder.CsysMirrorOptions.MirrorYAndZ;

                NXOpen.Features.Feature[] objects1 = features;
                bool added1;
                added1 = mirrorBuilder1.FeatureList.Add(objects1);


                NXOpen.Point3d coordinates2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                NXOpen.Point point2;
                point2 = workPart.Points.CreatePoint(coordinates2);

                mirrorBuilder1.ReferencePointService.Point = point2;


                mirrorBuilder1.PatternService.MirrorDefinition.PlaneOption = NXOpen.GeometricUtilities.MirrorPattern.PlaneOptions.New;

                plane1.SetMethod(NXOpen.PlaneTypes.MethodType.Distance);

                /*
                NXOpen.NXObject[] geom1 = new NXOpen.NXObject[1];
                NXOpen.Features.Block block1 = (NXOpen.Features.Block)workPart.Features.FindObject("BLOCK(1)");
                NXOpen.Face face1 = (NXOpen.Face)block1.FindObject("FACE 2 {(0,-4.699,2.2923) BLOCK(1)}");
                geom1[0] = face1;
                plane1.SetGeometry(geom1);
                */                 

                NXOpen.NXObject[] geom1 = new NXOpen.NXObject[1];
                NXOpen.DatumPlane datumPlane1 = mirrorPlane; //(NXOpen.DatumPlane)workPart.Datums.FindObject("DATUM_CSYS(0) XZ plane");
                geom1[0] = datumPlane1;
                plane1.SetGeometry(geom1);

                plane1.SetFlip(false);

                plane1.SetReverseSide(false);

                NXOpen.Expression expression1;
                expression1 = plane1.Expression;
                
                expression1.RightHandSide = (-breadth/2).ToString();

                plane1.SetAlternate(NXOpen.PlaneTypes.AlternateType.One);

                plane1.Evaluate();


                //NXOpen.Face face1 = mirrorFace;
                //mirrorBuilder1.PatternService.MirrorDefinition.ExistingPlane.Value = face1;

                mirrorBuilder1.ParentFeatureInternal = false;

                NXOpen.NXObject nXObject1;
                nXObject1 = mirrorBuilder1.Commit();

                mirrorFeature = (NXOpen.Features.Feature)nXObject1;

                mirrorBuilder1.Destroy();

                return true;
            }
            catch (Exception)
            {

                throw;
                return false;
            }
        }
    }

    public class ConectorDrawing
    {
        public static Session theSession = Session.GetSession();
        public static UFSession theUFSession = UFSession.GetUFSession();
        public static UI theUI = UI.GetUI();
        public static ListingWindow lw = theSession.ListingWindow;
        public static Part workPart = theSession.Parts.Work;

        public static void CreateDrawing()
        {
            // Switching Modeling to Drafting application
            theSession.ApplicationSwitchImmediate("UG_APP_DRAFTING");

            // Craete Drafting Sheet by calling the Function
            CreateSheet();

            // Base View Point
            Point3d baseViewPoint = new Point3d(115, 165, 0);
            BaseView baseView; // Base view 
            // Calling fuction to create Base View of Top
            CreateBaseView("Top", 0.2, baseViewPoint, out baseView);

            // Projected Front View Point
            Point3d frontViewPoint = new Point3d(115, 100, 0);
            ProjectedView projectedFrontiew; // projected Front View
            // Calling fuction to create Projected View of Front
            ProjectedView(baseView, frontViewPoint, out projectedFrontiew);

            // Projected Front View Point
            Point3d sideViewPoint = new Point3d(240, 165, 0);
            ProjectedView projectedsideView; // projected Front View
            // Calling fuction to create Projected View of Front
            ProjectedView(baseView, sideViewPoint, out projectedsideView);

            // Base View Point
            Point3d isometricViewPoint = new Point3d(240, 95, 0);
            BaseView isometricView; // Base view 
            // Calling fuction to create Base View of Top
            CreateBaseView("Isometric", 0.5, isometricViewPoint, out isometricView);


            ArrayList centerMarkList = new ArrayList();
            foreach (DisplayableObject objects in workPart.DrawingSheets.CurrentDrawingSheet.View.AskVisibleObjects())
            {
                if (objects.GetType() == typeof(NXOpen.Annotations.CenterMark))
                {
                    centerMarkList.Add(objects);
                }
                if (objects.GetType() == typeof(NXOpen.Annotations.Centerline3d))
                {
                    centerMarkList.Add(objects);
                }

            }
            // list1 = (Tag[])arr_list1.ToArray(typeof(Tag));
            DisplayableObject[] objects1 = (DisplayableObject[])centerMarkList.ToArray(typeof(DisplayableObject));

            theSession.DisplayManager.BlankObjects(objects1);
            workPart.Views.WorkView.FitAfterShowOrHide(NXOpen.View.ShowOrHideType.HideOnly);


            // Dictionary to Add Vertical and Horizontal Drafting Curves with their respective coordinate value
            Dictionary<DraftingCurve, double> VerticalDraftCurves = new Dictionary<DraftingCurve, double>();
            Dictionary<DraftingCurve, double> HorizontalDraftCurves = new Dictionary<DraftingCurve, double>();

            foreach (DraftingBody draftingBody in baseView.DraftingBodies)
            {
                foreach (DraftingCurve draftingCurve in draftingBody.DraftingCurves)
                {
                    double[] StartPoint = new double[3];// Array to store Start Point of Drafting Curve
                    double[] EndPoint = new double[3]; // Array to store End Point of Drafting Curve

                    // calling function to get Start Point from given Drafting Curve
                    GetCurveProps(draftingCurve, 0, out StartPoint);
                    // calling function to get End Point from given Drafting Curve
                    GetCurveProps(draftingCurve, 1, out EndPoint);

                    // Check Y-Coordinate point of Draft curve Start point and End Point are Same 
                    if (Math.Round(StartPoint[0], 2) == Math.Round(EndPoint[0], 2))
                    {
                        // Add Vertical Draft curves to the VerticalDraftCurves Dictionary
                        VerticalDraftCurves.Add(draftingCurve, StartPoint[0]);
                    }

                    // Check X-Coordinate point of Draft curve Start point and End Point are Same 
                    if (Math.Round(StartPoint[1], 2) == Math.Round(EndPoint[1], 2))
                    {
                        // Add Horizontal Draft points to the VerticalDraftCurves Dictionary
                        HorizontalDraftCurves.Add(draftingCurve, StartPoint[1]);
                    }


                }
            }

            // Order the dictionary base on the x and y coordinate value of respective drafting curves
            VerticalDraftCurves = VerticalDraftCurves.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            HorizontalDraftCurves = HorizontalDraftCurves.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            //Get information of Bounding box of the given draftView
            double[] boundaries = new double[4];
            theUFSession.Draw.AskViewBorders(baseView.Tag, boundaries); // Ufun for Bounding box of View

            // Bounding Box X and Y lengths
            double boundingBox_X_Length = boundaries[2] - boundaries[0];
            double boundingBox_Y_Length = boundaries[3] - boundaries[1];

            Point3d dimensionPosition = new Point3d(); // Point to position the dimension
            dimensionPosition.X = boundaries[0] + (boundingBox_X_Length / 2); // Point X value for Horizontal Dimension
            dimensionPosition.Y = boundaries[1]-20; // Point Y value for Horizontal Dimension
            dimensionPosition.Z = 0; // Point Z value

            // Horizontal Dimension for Base view by calling CreateDimension function
            CreateDimension(baseView, VerticalDraftCurves.First().Key, VerticalDraftCurves.Last().Key, dimensionPosition);

            dimensionPosition.X = boundaries[0] - 40;
            dimensionPosition.Y = boundaries[1] + (boundingBox_Y_Length / 2);
            // Vertical Dimension for Base view by calling CreateDimension function
            CreateDimension(baseView, HorizontalDraftCurves.First().Key, HorizontalDraftCurves.Last().Key, dimensionPosition);

            HorizontalDraftCurves.Clear();
            VerticalDraftCurves.Clear();

            foreach (DraftingBody draftingBody in projectedFrontiew.DraftingBodies)
            {
                foreach (DraftingCurve draftingCurve in draftingBody.DraftingCurves)
                {
                    double[] StartPoint = new double[3];// Array to store Start Point of Drafting Curve
                    double[] EndPoint = new double[3]; // Array to store End Point of Drafting Curve

                    // calling function to get Start Point from given Drafting Curve
                    GetCurveProps(draftingCurve, 0, out StartPoint);
                    // calling function to get End Point from given Drafting Curve
                    GetCurveProps(draftingCurve, 1, out EndPoint);

                    // Check Y-Coordinate point of Draft curve Start point and End Point are Same 
                    if (Math.Round(StartPoint[0], 2) == Math.Round(EndPoint[0], 2))
                    {
                        // Add Vertical Draft curves to the VerticalDraftCurves Dictionary
                        VerticalDraftCurves.Add(draftingCurve, StartPoint[0]);
                    }

                    // Check X-Coordinate point of Draft curve Start point and End Point are Same 
                    if (Math.Round(StartPoint[2], 2) == Math.Round(EndPoint[2], 2))
                    {
                        // Add Horizontal Draft points to the VerticalDraftCurves Dictionary
                        HorizontalDraftCurves.Add(draftingCurve, StartPoint[2]);
                    }
                }
            }

            // Order the dictionary base on the x and y coordinate value of respective drafting curves
            VerticalDraftCurves = VerticalDraftCurves.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            HorizontalDraftCurves = HorizontalDraftCurves.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);

            //Get information of Bounding box of the given draftView
            double[] boundaries1 = new double[4];
            theUFSession.Draw.AskViewBorders(projectedFrontiew.Tag, boundaries1); // Ufun for Bounding box of View

            // Bounding Box X and Y lengths
            boundingBox_X_Length = boundaries1[2] - boundaries1[0];
            boundingBox_Y_Length = boundaries1[3] - boundaries1[1];

            dimensionPosition.X = boundaries1[0] - 40;
            dimensionPosition.Y = boundaries1[1] + (boundingBox_Y_Length / 2);

            // Vertical Dimension for Base view by calling CreateDimension function
            CreateDimensionProjectedView(projectedFrontiew, HorizontalDraftCurves.First().Key, HorizontalDraftCurves.Last().Key, dimensionPosition);

            


        }

        /// <summary>
        /// Create Drafting in Drawing Application
        /// </summary>
        private static void CreateSheet()
        {
            // Drawing Sheet Builder
            NXOpen.Drawings.DraftingDrawingSheet nullNXOpen_Drawings_DraftingDrawingSheet = null;
            NXOpen.Drawings.DraftingDrawingSheetBuilder draftingDrawingSheetBuilder1;
            draftingDrawingSheetBuilder1 = workPart.DraftingDrawingSheets.CreateDraftingDrawingSheetBuilder(nullNXOpen_Drawings_DraftingDrawingSheet);

            draftingDrawingSheetBuilder1.AutoStartViewCreation = true;

            draftingDrawingSheetBuilder1.Height = 841.0;

            draftingDrawingSheetBuilder1.Length = 1189.0;

            draftingDrawingSheetBuilder1.StandardMetricScale = NXOpen.Drawings.DrawingSheetBuilder.SheetStandardMetricScale.S11;

            draftingDrawingSheetBuilder1.StandardEnglishScale = NXOpen.Drawings.DrawingSheetBuilder.SheetStandardEnglishScale.S11;

            draftingDrawingSheetBuilder1.ScaleNumerator = 1.0;

            draftingDrawingSheetBuilder1.ScaleDenominator = 1.0;

            draftingDrawingSheetBuilder1.Units = NXOpen.Drawings.DrawingSheetBuilder.SheetUnits.Metric;

            draftingDrawingSheetBuilder1.ProjectionAngle = NXOpen.Drawings.DrawingSheetBuilder.SheetProjectionAngle.Third;

            draftingDrawingSheetBuilder1.Number = "1";

            draftingDrawingSheetBuilder1.SecondaryNumber = "";

            draftingDrawingSheetBuilder1.Revision = "A";

            // File path of NX installation
            string nxDirectory = theSession.GetEnvironmentVariableValue("UGII_BASE_DIR");
            // Adding file and file path of the Drawing Template
            draftingDrawingSheetBuilder1.MetricSheetTemplateLocation = nxDirectory + @"\DRAFTING\templates\Drawing-A4-Size2D-template.prt";

            // Commit the Builder
            NXOpen.NXObject nXObject1;
            nXObject1 = draftingDrawingSheetBuilder1.Commit();

            // Destroy the Builder
            draftingDrawingSheetBuilder1.Destroy();
        }
        /// <summary>
        /// Create Base View
        /// </summary>
        /// <param name="ViewName">View Name</param>
        /// <param name="Scale">Scale</param>
        /// <param name="ViewPos">View Position in Drawing sheet</param>
        /// <param name="baseView">Out Create Base View in Drawing Sheet</param>
        /// <returns></returns>
        private static bool CreateBaseView(String ViewName, double Scale, Point3d ViewPos, out BaseView baseView)
        {
            baseView = null; // Initialize Base view
            try
            {
                // Declare and Initialize BaseView Class Object
                NXOpen.Drawings.BaseView nullNXOpen_Drawings_BaseView = null;

                // Declare BaseViewBuilder class Object
                NXOpen.Drawings.BaseViewBuilder baseViewBuilder1;
                //Initialize Object for baseViewBuilder1
                baseViewBuilder1 = workPart.DraftingViews.CreateBaseViewBuilder(nullNXOpen_Drawings_BaseView);

                baseViewBuilder1.Placement.Associative = true;

                // Passing View to Create that View(Eg: "Top","Front")
                NXOpen.ModelingView modelingView1 = (NXOpen.ModelingView)workPart.ModelingViews.FindObject(ViewName);
                baseViewBuilder1.SelectModelView.SelectedView = modelingView1;

                baseViewBuilder1.SecondaryComponents.ObjectType = NXOpen.Drawings.DraftingComponentSelectionBuilder.Geometry.PrimaryGeometry;

                baseViewBuilder1.Style.ViewStyleBase.Part = workPart;

                baseViewBuilder1.Style.ViewStyleBase.PartName = workPart.FullPath;

                bool loadStatus1;
                loadStatus1 = workPart.IsFullyLoaded;

                baseViewBuilder1.SelectModelView.SelectedView = modelingView1;

                // Scale of the Drawing
                baseViewBuilder1.Scale.Denominator = Scale;

                // View Position in Drawing sheet
                baseViewBuilder1.Placement.Placement.SetValue(null, workPart.Views.WorkView, ViewPos);

                NXOpen.NXObject nXObject1; // Feature Object Output
                // Commits the feature parameters and Creates the NXObject
                nXObject1 = baseViewBuilder1.Commit();
                // Convert the Nxobject to Base View
                baseView = (BaseView)nXObject1;

                // Destroy the Builder. It deletes the builder, and the cleans up any objects create by the Builder  
                baseViewBuilder1.Destroy();

                return true;
            }
            catch (Exception ex)
            {
                return false;
                throw;
            }
        }
        /// <summary>
        /// Create Projected View from a given Base View and Position in Drawing Sheet
        /// </summary>
        /// <param name="baseView">Base View</param>
        /// <param name="viewPos">View Position in Drawing sheet</param>
        /// <param name="projectedView">Out Create Projected View in Drawing Sheet</param>
        /// <returns></returns>
        public static bool ProjectedView(BaseView baseView, Point3d viewPos, out ProjectedView projectedView)
        {
            try
            {
                // Declare and Initialize ProjectedView Class Object
                NXOpen.Drawings.ProjectedView nullNXOpen_Drawings_ProjectedView = null;

                // Declare ProjectedViewBuilder class Object
                NXOpen.Drawings.ProjectedViewBuilder projectedViewBuilder1;
                //Initialize Object for projectedViewBuilder1
                projectedViewBuilder1 = workPart.DraftingViews.CreateProjectedViewBuilder(nullNXOpen_Drawings_ProjectedView);

                projectedViewBuilder1.Placement.Associative = true;

                projectedViewBuilder1.Placement.AlignmentMethod = NXOpen.Drawings.ViewPlacementBuilder.Method.PerpendicularToHingeLine;

                NXOpen.Direction nullNXOpen_Direction = null;
                projectedViewBuilder1.Placement.AlignmentVector = nullNXOpen_Direction;

                projectedViewBuilder1.Placement.AlignmentOption = NXOpen.Drawings.ViewPlacementBuilder.Option.ModelPoint;

                projectedViewBuilder1.SecondaryComponents.ObjectType = NXOpen.Drawings.DraftingComponentSelectionBuilder.Geometry.PrimaryGeometry;

                // Pass the Base View to Create Projected View.
                projectedViewBuilder1.Parent.View.Value = baseView;

                // Get the file location of the Work Part
                projectedViewBuilder1.Style.ViewStyleBase.PartName = workPart.FullPath;

                //NXOpen.Point3d vieworigin1 = new NXOpen.Point3d(-7.0, 6.5, -52.5);
                //projectedViewBuilder1.Style.ViewStylePerspective.ViewOrigin = vieworigin1;

                NXOpen.Point3d point2 = new NXOpen.Point3d(0, 0, 0.0);
                NXOpen.Point point1 = workPart.Points.CreatePoint(point2);
                projectedViewBuilder1.Placement.AlignmentPoint.SetValue(point1, baseView, point2);

                // Get the scale of the given base view
                double numerator, denominator, scaleValue;
                string viewScale = baseView.Style.General.Scale.ToString();
                scaleValue = Convert.ToDouble(viewScale);
                if (scaleValue >= 1)
                {
                    numerator = scaleValue;
                    denominator = 1;
                }
                else
                {
                    numerator = 1;
                    denominator = 1 / scaleValue;
                }

                projectedViewBuilder1.Style.ViewStyleGeneral.Scale.Numerator = numerator;

                NXOpen.Assemblies.Arrangement nullNXOpen_Assemblies_Arrangement = null;
                projectedViewBuilder1.Style.ViewStyleBase.Arrangement.SelectedArrangement = nullNXOpen_Assemblies_Arrangement;

                projectedViewBuilder1.Style.ViewStyleBase.Arrangement.InheritArrangementFromParent = true;

                projectedViewBuilder1.Placement.AlignmentView.Value = baseView;

                // Projected View Position in Drawing sheet
                projectedViewBuilder1.Placement.Placement.SetValue(null, workPart.Views.WorkView, viewPos);

                NXOpen.NXObject nXObject1; // Feature Object Output
                // Commits the feature parameters and Creates the NXObject
                nXObject1 = projectedViewBuilder1.Commit();
                // Convert the Nxobject to Projected View 
                projectedView = (ProjectedView)nXObject1;

                // Destroy the Builder. It deletes the builder, and the cleans up any objects create by the Builder
                projectedViewBuilder1.Destroy();

                return true;
            }
            catch (Exception ex)
            {
                projectedView = null;
                return false;
            }
        }
        /// <summary>
        /// Get Curve Properties
        /// </summary>
        /// <param name="CurveID">CurveID</param>
        /// <param name="Pram">Prameters 
        ///  Value is 0 for Start Point of Curve,
        ///  Prameters Value is 1 for End Point of Curve
        /// </param>
        /// <param name="Point">Out Position of Point as Double Array</param>
        /// <returns></returns>
        public static bool GetCurveProps(NXObject CurveID, double Pram, out double[] Point)
        {
            Point = new double[3];
            try
            {
                double[] Tangent = new double[3];
                double[] P_Norm = new double[3];
                double[] B_Norm = new double[3];
                double Torsion;
                double Rad_of_Curve;
                // Pram Value is 0 for Start Point of Curve
                // Pram Value is 1 for End Point of Curve

                // Ufunc to get the Drafting Curves Properties
                theUFSession.Modl.AskCurveProps(CurveID.Tag, Pram, Point, Tangent, P_Norm, B_Norm, out Torsion, out Rad_of_Curve);

                return true;
            }
            catch (Exception ex)
            {
                throw;
                return false;
            }
        }
        /// <summary>
        /// Create Dimension Base View
        /// </summary>
        /// <param name="draftingView">Drafting View is Base View</param>
        /// <param name="draftingCurve1">Drafting Curve 1</param>
        /// <param name="draftingCurve2">Drafting Curve 2</param>
        /// <param name="DimPosition">Dimension Positon</param>
        /// <returns></returns>
        public static bool CreateDimension(DraftingView draftingView, DraftingCurve draftingCurve1, DraftingCurve draftingCurve2, Point3d DimPosition)
        {
            try
            {
                // Declare and Initialize Dimension Class Object
                NXOpen.Annotations.Dimension nullNXOpen_Annotations_Dimension = null;
                // Declare RapidDimensionBuilder class Object
                NXOpen.Annotations.RapidDimensionBuilder rapidDimensionBuilder1;
                //Initialize Object for rapidDimensionBuilder1
                rapidDimensionBuilder1 = workPart.Dimensions.CreateRapidDimensionBuilder(nullNXOpen_Annotations_Dimension);

                string[] lines1 = new string[0];
                rapidDimensionBuilder1.AppendedText.SetBefore(lines1);

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                rapidDimensionBuilder1.Origin.Anchor = NXOpen.Annotations.OriginBuilder.AlignmentPosition.MidCenter;

                rapidDimensionBuilder1.Origin.Plane.PlaneMethod = NXOpen.Annotations.PlaneBuilder.PlaneMethodType.XyPlane;

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                NXOpen.Annotations.DimensionUnit dimensionlinearunits1; // check these are usefull are not
                dimensionlinearunits1 = rapidDimensionBuilder1.Style.UnitsStyle.DimensionLinearUnits;

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                NXOpen.Direction nullNXOpen_Direction = null;
                rapidDimensionBuilder1.Measurement.Direction = nullNXOpen_Direction;

                NXOpen.View nullNXOpen_View = null;
                rapidDimensionBuilder1.Measurement.DirectionView = nullNXOpen_View;

                rapidDimensionBuilder1.Style.DimensionStyle.NarrowDisplayType = NXOpen.Annotations.NarrowDisplayOption.None;

                //NXOpen.Drawings.BaseView baseView1 = (NXOpen.Drawings.BaseView)workPart.DraftingViews.FindObject("Front@1");

                // Pass Base view
                NXOpen.Drawings.BaseView baseView1 = (BaseView)draftingView;

                double[] point01 = new double[3]; // Declare of point 

                // Ufunc to get the Drafting Curves Properties
                theUFSession.Modl.AskCurvePoints(draftingCurve1.Tag, 1, 1, 1, out int Num, out point01);

                // Create point3d using point01  
                NXOpen.Point3d point1_1 = new NXOpen.Point3d(point01[0], point01[1], point01[2]);
                NXOpen.Point3d point2_1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.FirstAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, draftingCurve1, baseView1, point1_1, null, nullNXOpen_View, point2_1);

                NXOpen.Assemblies.Component nullNXOpen_Assemblies_Component = null;
                rapidDimensionBuilder1.Measurement.PartOccurrence = nullNXOpen_Assemblies_Component;

                double[] point02 = new double[3]; // Declare of point to get point on the Drafting Curve

                // Ufunc to get the Drafting Curves Properties
                theUFSession.Modl.AskCurvePoints(draftingCurve2.Tag, 1, 1, 1, out int Num1, out point02);

                // Create point3d using point02
                NXOpen.Point3d point1_2 = new NXOpen.Point3d(point02[0], point02[1], point02[2]);
                NXOpen.Point3d point2_2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.SecondAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, draftingCurve2, baseView1, point1_2, null, nullNXOpen_View, point2_2);
                /*
                NXOpen.Point3d point1_3 = new NXOpen.Point3d(-50.0, -37.5, 50.0);
                NXOpen.Point3d point2_3 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.FirstAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, draftingCurve1, baseView1, point1_3, null, nullNXOpen_View, point2_3);

                NXOpen.Point3d point1_4 = new NXOpen.Point3d(50.0, -37.5, 50.0);
                NXOpen.Point3d point2_4 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.SecondAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, draftingCurve2, baseView1, point1_4, null, nullNXOpen_View, point2_4);
                */
                rapidDimensionBuilder1.Measurement.PartOccurrence = nullNXOpen_Assemblies_Component;

                NXOpen.Annotations.Annotation.AssociativeOriginData assocOrigin1 = new NXOpen.Annotations.Annotation.AssociativeOriginData();
                assocOrigin1.OriginType = NXOpen.Annotations.AssociativeOriginType.Drag;
                assocOrigin1.View = nullNXOpen_View;
                assocOrigin1.ViewOfGeometry = nullNXOpen_View;
                NXOpen.Point nullNXOpen_Point = null;
                assocOrigin1.PointOnGeometry = nullNXOpen_Point;
                NXOpen.Annotations.Annotation nullNXOpen_Annotations_Annotation = null;
                assocOrigin1.VertAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.VertAlignmentPosition = NXOpen.Annotations.AlignmentPosition.TopLeft;
                assocOrigin1.HorizAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.HorizAlignmentPosition = NXOpen.Annotations.AlignmentPosition.TopLeft;
                assocOrigin1.AlignedAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.DimensionLine = 0;
                assocOrigin1.AssociatedView = nullNXOpen_View;
                assocOrigin1.AssociatedPoint = nullNXOpen_Point;
                assocOrigin1.OffsetAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.OffsetAlignmentPosition = NXOpen.Annotations.AlignmentPosition.TopLeft;
                assocOrigin1.XOffsetFactor = 0.0;
                assocOrigin1.YOffsetFactor = 0.0;
                assocOrigin1.StackAlignmentPosition = NXOpen.Annotations.StackAlignmentPosition.Above;
                rapidDimensionBuilder1.Origin.SetAssociativeOrigin(assocOrigin1);

                //NXOpen.Point3d point1 = new NXOpen.Point3d(104.65459431345354, 164.33347780859916, 0.0);
                rapidDimensionBuilder1.Origin.Origin.SetValue(null, nullNXOpen_View, DimPosition);

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                rapidDimensionBuilder1.Style.LineArrowStyle.LeaderOrientation = NXOpen.Annotations.LeaderSide.Right;

                rapidDimensionBuilder1.Style.DimensionStyle.TextCentered = true;

                NXOpen.NXObject nXObject1;
                nXObject1 = rapidDimensionBuilder1.Commit(); // Commits the rapidDimensionBuilder1

                rapidDimensionBuilder1.Destroy(); // Destroy the rapidDimensionBuilder1

                return true;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Create Dimension for Projected View
        /// </summary>
        /// <param name="draftingView">Drafting View is Projected View</param>
        /// <param name="draftingCurve1">Drafting Curve 1</param>
        /// <param name="draftingCurve2">Drafting Curve 2</param>
        /// <param name="DimPosition">Dimension Position</param>
        /// <returns></returns>
        public static bool CreateDimensionProjectedView(DraftingView draftingView, DraftingCurve draftingCurve1, DraftingCurve draftingCurve2, Point3d DimPosition)
        {
            try
            {
                // Declare and Initialize Dimension Class Object
                NXOpen.Annotations.Dimension nullNXOpen_Annotations_Dimension = null;
                // Declare RapidDimensionBuilder class Object
                NXOpen.Annotations.RapidDimensionBuilder rapidDimensionBuilder1;
                //Initialize Object for rapidDimensionBuilder1
                rapidDimensionBuilder1 = workPart.Dimensions.CreateRapidDimensionBuilder(nullNXOpen_Annotations_Dimension);

                string[] lines1 = new string[0];
                rapidDimensionBuilder1.AppendedText.SetBefore(lines1);

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                rapidDimensionBuilder1.Origin.Anchor = NXOpen.Annotations.OriginBuilder.AlignmentPosition.MidCenter;

                rapidDimensionBuilder1.Origin.Plane.PlaneMethod = NXOpen.Annotations.PlaneBuilder.PlaneMethodType.XyPlane;

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                NXOpen.Direction nullNXOpen_Direction = null;
                rapidDimensionBuilder1.Measurement.Direction = nullNXOpen_Direction;

                NXOpen.View nullNXOpen_View = null;
                rapidDimensionBuilder1.Measurement.DirectionView = nullNXOpen_View;

                rapidDimensionBuilder1.Style.DimensionStyle.NarrowDisplayType = NXOpen.Annotations.NarrowDisplayOption.None;

                //NXOpen.Drawings.BaseView baseView1 = (NXOpen.Drawings.BaseView)workPart.DraftingViews.FindObject("Front@1");

                // Pass ProjectedV iew 
                NXOpen.Drawings.ProjectedView baseView1 = (ProjectedView)draftingView;

                //NXOpen.Drawings.DraftingBody draftingBody1 = (NXOpen.Drawings.DraftingBody)baseView1.DraftingBodies.FindObject("0 EXTRUDE(4)  0");
                //NXOpen.Drawings.DraftingCurve draftingCurve1 = (NXOpen.Drawings.DraftingCurve)draftingBody1.DraftingCurves.FindObject("(Extracted Edge) EDGE * 140 * 150 {(-50,-37.5,50)(-50,-37.5,25)(-50,-37.5,0) EXTRUDE(4)}");
                //NXOpen.Point3d point1_1 = new NXOpen.Point3d(-50.0, -37.5, 50.0);


                double[] CurvePoint_1 = new double[3];// Declare of CurvePoint_1 to get the point on the drafting curve
                // Ufunc to get the Drafting Curves Properties
                theUFSession.Modl.AskCurvePoints(draftingCurve1.Tag, 1, 1, 1, out int Num1, out CurvePoint_1);
                // Create point3d using CurvePoint_1 
                NXOpen.Point3d point1_1 = new NXOpen.Point3d(CurvePoint_1[0], CurvePoint_1[1], CurvePoint_1[2]);
                NXOpen.Point3d point2_1 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.FirstAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, draftingCurve1, baseView1, point1_1, null, nullNXOpen_View, point2_1);

                NXOpen.Assemblies.Component nullNXOpen_Assemblies_Component = null;
                // rapidDimensionBuilder1.Measurement.PartOccurrence = nullNXOpen_Assemblies_Component;

                //NXOpen.Point3d point1_2 = new NXOpen.Point3d(-50.0, -37.5, 0.0);


                double[] CurvePoint_2 = new double[3]; // Declare of CurvePoint_2 
                // Ufunc to get the Drafting Curves Properties
                theUFSession.Modl.AskCurvePoints(draftingCurve2.Tag, 1, 1, 1, out int Num2, out CurvePoint_2);

                // Create point3d using CurvePoint_2 
                NXOpen.Point3d point1_2 = new NXOpen.Point3d(CurvePoint_2[0], CurvePoint_2[1], CurvePoint_2[2]);
                NXOpen.Point3d point2_2 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.SecondAssociativity.SetValue(NXOpen.InferSnapType.SnapType.End, draftingCurve2, baseView1, point1_2, null, nullNXOpen_View, point2_2);
                /*
                NXOpen.Point3d point1_3 = new NXOpen.Point3d(-50.0, -37.5, 50.0);
                NXOpen.Point3d point2_3 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.FirstAssociativity.SetValue(NXOpen.InferSnapType.SnapType.Start, draftingCurve1, baseView1, point1_3, null, nullNXOpen_View, point2_3);

                NXOpen.Point3d point1_4 = new NXOpen.Point3d(-50.0, -37.5, 0.0);
                NXOpen.Point3d point2_4 = new NXOpen.Point3d(0.0, 0.0, 0.0);
                rapidDimensionBuilder1.SecondAssociativity.SetValue(NXOpen.InferSnapType.SnapType.End, draftingCurve1, baseView1, point1_4, null, nullNXOpen_View, point2_4);
                */
                rapidDimensionBuilder1.Measurement.PartOccurrence = nullNXOpen_Assemblies_Component;
                //Guide.InfoWriteLine("Curve 1 points \"" + CurvePoint_1[0] + "," + CurvePoint_1[1] + "," + CurvePoint_1[2]);
                //Guide.InfoWriteLine("Curve 2 points \"" + CurvePoint_2[0] + "," + CurvePoint_2[1] + "," + CurvePoint_2[2]);


                NXOpen.Annotations.Annotation.AssociativeOriginData assocOrigin1 = new NXOpen.Annotations.Annotation.AssociativeOriginData();
                assocOrigin1.OriginType = NXOpen.Annotations.AssociativeOriginType.Drag;
                assocOrigin1.View = nullNXOpen_View;
                assocOrigin1.ViewOfGeometry = nullNXOpen_View;
                NXOpen.Point nullNXOpen_Point = null;
                assocOrigin1.PointOnGeometry = nullNXOpen_Point;
                NXOpen.Annotations.Annotation nullNXOpen_Annotations_Annotation = null;
                assocOrigin1.VertAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.VertAlignmentPosition = NXOpen.Annotations.AlignmentPosition.TopLeft;
                assocOrigin1.HorizAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.HorizAlignmentPosition = NXOpen.Annotations.AlignmentPosition.TopLeft;
                assocOrigin1.AlignedAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.DimensionLine = 0;
                assocOrigin1.AssociatedView = nullNXOpen_View;
                assocOrigin1.AssociatedPoint = nullNXOpen_Point;
                assocOrigin1.OffsetAnnotation = nullNXOpen_Annotations_Annotation;
                assocOrigin1.OffsetAlignmentPosition = NXOpen.Annotations.AlignmentPosition.TopLeft;
                assocOrigin1.XOffsetFactor = 0.0;
                assocOrigin1.YOffsetFactor = 0.0;
                assocOrigin1.StackAlignmentPosition = NXOpen.Annotations.StackAlignmentPosition.Above;
                rapidDimensionBuilder1.Origin.SetAssociativeOrigin(assocOrigin1);

                //NXOpen.Point3d point1 = new NXOpen.Point3d(51.8, 205, 0.0); 

                // Position of the dimension 
                Point3d point1 = DimPosition;

                rapidDimensionBuilder1.Origin.Origin.SetValue(null, nullNXOpen_View, point1);

                rapidDimensionBuilder1.Origin.SetInferRelativeToGeometry(false);

                rapidDimensionBuilder1.Style.LineArrowStyle.LeaderOrientation = NXOpen.Annotations.LeaderSide.Right;

                rapidDimensionBuilder1.Style.DimensionStyle.TextCentered = true;

                NXOpen.NXObject nXObject1;
                nXObject1 = rapidDimensionBuilder1.Commit(); // Commits the rapidDimensionBuilder1

                rapidDimensionBuilder1.Destroy(); // Destroy the rapidDimensionBuilder1

                return true;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


    }
}
