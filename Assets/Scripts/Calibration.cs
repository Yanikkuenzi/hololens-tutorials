// Using code from https://github.com/microsoft/psi/blob/master/Sources/Calibration/Microsoft.Psi.Calibration/CalibrationExtensions.cs


using System;
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Spatial.Euclidean;
using MathNet.Spatial.Units;

public static class Calibration
{
        /// <summary>
        /// Estimate a camera's camera matrix and distortion coefficients, given a set of image points and
        /// corresponding 3d camera points. The underlying calibration procedure utilizes Levenberg Marquardt
        /// optimization to produce these estimates.
        /// </summary>
        /// <param name="cameraPoints">3d positions of the points in camera coordinates.
        /// These points are *not* yet in the typically assumed \psi basis (X=Forward, Y=Left, Z=Up).
        /// Instead, we assume that X and Y correspond to directions in the image plane, and Z corresponds to depth in the plane.</param>
        /// <param name="imagePoints">2d positions of the points in the image.</param>
        /// <param name="initialCameraMatrix">Initial estimate of the camera matrix.</param>
        /// <param name="initialDistortionCoefficients">Initial estimate of distortion coefficients.</param>
        /// <param name="cameraMatrix">Estimated output camera matrix.</param>
        /// <param name="distortionCoefficients">Estimated output distortion coefficients.</param>
        /// <param name="silent">If false, print debugging information to the console.</param>
        /// <returns>The RMS (root mean squared) error of this computation.</returns>
        public static double CalibrateCameraIntrinsics(
            List<Point3D> cameraPoints,
            List<Point2D> imagePoints,
            Matrix<double> initialCameraMatrix,
            Vector<double> initialDistortionCoefficients,
            out Matrix<double> cameraMatrix,
            out Vector<double> distortionCoefficients,
            bool silent = true)
        {
            // pack parameters into vector
            // parameters: fx, fy, cx, cy, k1, k2 = 6 parameters
            //var initialParameters = Vector<double>.Build.Dense(6);
            var initialParameters = Vector<double>.Build.Dense(4);
            int pi = 0;
            initialParameters[pi++] = initialCameraMatrix[0, 0]; // fx
            initialParameters[pi++] = initialCameraMatrix[1, 1]; // fy
            initialParameters[pi++] = initialCameraMatrix[0, 2]; // cx
            initialParameters[pi++] = initialCameraMatrix[1, 2]; // cy
            //initialParameters[pi++] = initialDistortionCoefficients[0]; // k1
            //initialParameters[pi++] = initialDistortionCoefficients[1]; // k2

            var error = CalibrateCamera(cameraPoints, imagePoints, initialParameters, false, out var computedParameters, silent);

            // unpack parameters into the outputs
            cameraMatrix = Matrix<double>.Build.Dense(3, 3);
            distortionCoefficients = Vector<double>.Build.Dense(2);

            pi = 0;
            cameraMatrix[0, 0] = computedParameters[pi++]; // fx
            cameraMatrix[1, 1] = computedParameters[pi++]; // fy
            cameraMatrix[2, 2] = 1;
            cameraMatrix[0, 2] = computedParameters[pi++]; // cx
            cameraMatrix[1, 2] = computedParameters[pi++]; // cy

            // TODO: change
            distortionCoefficients[0] = -0.22356547153865305;
            distortionCoefficients[1] = 0.03186705146154515;
                                                              // distortionCoefficients[0] = computedParameters[pi++]; // k1
                                                              // distortionCoefficients[1] = computedParameters[pi++]; // k2

        return error;
        }

        private static double CalibrateCamera(
            List<Point3D> worldPoints,
            List<Point2D> imagePoints,
            Vector<double> initialParameters,
            bool computeExtrinsics,
            out Vector<double> outputParameters,
            bool silent = true)
        {
            int numValues = worldPoints.Count;

            // create a new vector for computing and returning our final parameters
            var parametersCount = initialParameters.Count;
            outputParameters = Vector<double>.Build.Dense(parametersCount);
            initialParameters.CopyTo(outputParameters);

            // This is the function that gets passed to the Levenberg-Marquardt optimizer
            Vector<double> OptimizationFunction(Vector<double> p)
            {
                // initialize the error vector
                var fvec = Vector<double>.Build.Dense(numValues * 2);  // each component (x,y) is a separate entry

                // unpack parameters
                int pi = 0;

                // camera matrix
                var k = Matrix<double>.Build.DenseIdentity(3, 3);
                k[0, 0] = p[pi++]; // fx
                k[1, 1] = p[pi++]; // fy
                k[2, 2] = 1;
                k[0, 2] = p[pi++]; // cx
                k[1, 2] = p[pi++]; // cy

                // distortion coefficients
                var d = Vector<double>.Build.Dense(2, 0);
            // TODO: change back
            //d[0] = p[pi++]; // k1
            //d[1] = p[pi++]; // k2
            d[0] = -0.22356547153865305;
            d[1] = 0.03186705146154515;

                Matrix<double> rotationMatrix = null;
                Vector<double> translationVector = null;

                if (computeExtrinsics)
                {
                    // If we are computing extrinsics, that means the world points are not in local
                    // camera coordinates, so we need to also compute rotation and translation
                    var r = Vector<double>.Build.Dense(3);
                    r[0] = p[pi++];
                    r[1] = p[pi++];
                    r[2] = p[pi++];
                    rotationMatrix = AxisAngleToMatrix(r);

                    translationVector = Vector<double>.Build.Dense(3);
                    translationVector[0] = p[pi++];
                    translationVector[1] = p[pi++];
                    translationVector[2] = p[pi++];
                }

                int fveci = 0;
                for (int i = 0; i < numValues; i++)
                {
                    Point3D cameraPoint;
                    if (computeExtrinsics)
                    {
                        // transform world point to local camera coordinates
                        var x = rotationMatrix * worldPoints[i].ToVector();
                        x += translationVector;
                        cameraPoint = new Point3D(x[0], x[1], x[2]);
                    }
                    else
                    {
                        // world points are already in local camera coordinates
                        cameraPoint = worldPoints[i];
                    }

                    // fvec_i = y_i - f(x_i)
                    Project(k, d, cameraPoint, out Point2D projectedPoint);

                    var imagePoint = imagePoints[i];
                    fvec[fveci++] = imagePoint.X - projectedPoint.X;
                    fvec[fveci++] = imagePoint.Y - projectedPoint.Y;
                }

                return fvec;
            }

            // optimize
            var calibrate = new LevenbergMarquardt(OptimizationFunction);
            while (calibrate.State == LevenbergMarquardt.States.Running)
            {
                var rmsError = calibrate.MinimizeOneStep(outputParameters);
                if (!silent)
                {
                    Console.WriteLine("rms error = " + rmsError);
                }
            }

            if (!silent)
            {
                for (int i = 0; i < parametersCount; i++)
                {
                    Console.WriteLine(outputParameters[i] + "\t");
                }

                Console.WriteLine();
            }

            return calibrate.RMSError;
        }

        /// <summary>
        /// Project a 3D point (x, y, z) into a camera space (u, v) given the camera matrix and distortion coefficients.
        /// The 3D point is *not* yet in the typically assumed \psi basis (X=Forward, Y=Left, Z=Up).
        /// Instead, X and Y correspond to the image plane X and Y directions, with Z as depth.
        /// </summary>
        /// <param name="cameraMatrix">The camera matrix.</param>
        /// <param name="distCoeffs">The distortion coefficients of the camera.</param>
        /// <param name="point">Input 3D point (X and Y correspond to image dimensions, with Z as depth).</param>
        /// <param name="projectedPoint">Projected 2D point (output).</param>
        public static void Project(Matrix<double> cameraMatrix, Vector<double> distCoeffs, Point3D point, out Point2D projectedPoint)
        {
            double xp = point.X / point.Z;
            double yp = point.Y / point.Z;

            double fx = cameraMatrix[0, 0];
            double fy = cameraMatrix[1, 1];
            double cx = cameraMatrix[0, 2];
            double cy = cameraMatrix[1, 2];
            double k1 = distCoeffs[0];
            double k2 = distCoeffs[1];

            // compute f(xp, yp)
            double rsquared = xp * xp + yp * yp;
            double g = 1 + k1 * rsquared + k2 * rsquared * rsquared;
            double xpp = xp * g;
            double ypp = yp * g;
            projectedPoint = new Point2D(fx * xpp + cx, fy * ypp + cy);
        }

        /// <summary>
        /// Use the Rodrigues formula for transforming a given rotation from axis-angle representation to a 3x3 matrix.
        /// Where 'r' is a rotation vector:
        /// theta = norm(r)
        /// M = skew(r/theta)
        /// R = I + M * sin(theta) + M*M * (1-cos(theta)).
        /// </summary>
        /// <param name="vectorRotation">Rotation in axis-angle vector representation,
        /// where the angle is represented by the length (L2-norm) of the vector.</param>
        /// <returns>Rotation in a 3x3 matrix representation.</returns>
        public static Matrix<double> AxisAngleToMatrix(Vector<double> vectorRotation)
        {
            if (vectorRotation.Count != 3)
            {
                throw new InvalidOperationException("The input must be a valid 3-element vector representing an axis-angle rotation.");
            }

            double theta = vectorRotation.L2Norm();

            var matR = Matrix<double>.Build.DenseIdentity(3, 3);

            // if there is no rotation (theta == 0) return identity rotation
            if (theta == 0)
            {
                return matR;
            }

            // Create a skew-symmetric matrix from the normalized axis vector
            var rn = vectorRotation.Normalize(2);
            var matM = Matrix<double>.Build.Dense(3, 3);
            matM[0, 0] = 0;
            matM[0, 1] = -rn[2];
            matM[0, 2] = rn[1];
            matM[1, 0] = rn[2];
            matM[1, 1] = 0;
            matM[1, 2] = -rn[0];
            matM[2, 0] = -rn[1];
            matM[2, 1] = rn[0];
            matM[2, 2] = 0;

            // I + M * sin(theta) + M*M * (1 - cos(theta))
            var sinThetaM = matM * Math.Sin(theta);
            matR += sinThetaM;
            var matMM = matM * matM;
            var cosThetaMM = matMM * (1 - Math.Cos(theta));
            matR += cosThetaMM;

            return matR;
        }
}
