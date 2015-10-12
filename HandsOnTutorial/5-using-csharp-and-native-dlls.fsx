﻿(*** hide ***)
#load "ThespianCluster.fsx"
//#load "AzureCluster.fsx"

// Note: Before running, choose your cluster version at the top of this script.
// If necessary, edit credentials.fsx to enter your connection strings.

open System
open System.IO
open MBrace.Core
open MBrace.Flow

// Initialize client object to an MBrace cluster
let cluster = Config.GetCluster() 

(**

# Using C# DLLs and Native Components 

It is very simple to use C# DLLs, native DLLs and any nuget packages in your cloud computations.
For C# DLLs you just download and reference the packages as normal
in your F# scripting or other client application. The DLLs for the packages are automatically uploaded to 
the cloud workers as needed.  In a sense, you don't need to do anything special.

In this tutorial, you first reference some C# DLLs from the Math.NET NuGet package.
You also use native binaries from the Intel MKL library.
  
*)


(** First, reference and use the packages on the local machine *) 

#load @"../packages/MathNet.Numerics.FSharp/MathNet.Numerics.fsx"

open MathNet.Numerics
open MathNet.Numerics.LinearAlgebra

let matrix1 = Matrix<double>.Build.Random(10,10)
let vector1 = Vector<double>.Build.Random(10)

let product = vector1 * matrix1 

let check = (matrix1 * matrix1.Inverse()).Determinant()

(** Next, run the code on MBrace. Note that the DLLs from the packages are uploaded automatically. *)

cluster.ShowProcesses()
cluster.ShowWorkers()

(** Invert 100 150x150 matrices using managed code: *) 
let managedMathJob = 
    [| 1 .. 100 |]
    |> CloudFlow.OfArray
    |> CloudFlow.map (fun i -> 
            Control.UseManaged()
            let m = Matrix<double>.Build.Random(200,200) 
            (m * m.Inverse()).Determinant())
    |> CloudFlow.sum
    |> cluster.CreateProcess

// Show the progress
managedMathJob.ShowInfo()


// Await the result, we expect ~100.0
let managedMathResults = managedMathJob.Result


(** Next, run the code on MBrace using the MKL native DLLs. Note that 
for the moment we manage the upload of the native DLLs explicitly, placing
them in the temporary storage on the worker.   

To upload DLLs, register their paths as native dependencies
These will be included with all uploaded dependencies of the session 
*)

let contentDir = __SOURCE_DIRECTORY__ + "/../packages/MathNet.Numerics.MKL.Win-x64/content/"
cluster.RegisterNativeDependency (contentDir + "libiomp5md.dll")
cluster.RegisterNativeDependency (contentDir + "MathNet.Numerics.MKL.dll")

(** The first MKL job can take a while first time you run it, because 'MathNet.Numerics.MKL.dll' is 41MB and needs to be uploaded: *) 
let firstMklJob = 
    cloud { 
        Control.UseNativeMKL()
        let m = Matrix<double>.Build.Random(200,200) 
        return (m * m.Inverse()).Determinant()
    }
    |> cluster.CreateProcess

// Check progress
firstMklJob.ShowInfo()

// Wait for the result
firstMklJob.Result

(** Now run a much larger job: 1000 200x200 matrices, inverted using Intel MKL: *)
let nativeMathJob = 
    [| 1 .. 1000 |]
    |> CloudFlow.OfArray
    |> CloudFlow.map (fun i -> 
            Control.UseNativeMKL()
            let m = Matrix<double>.Build.Random(200,200) 
            (m * m.Inverse()).Determinant())
    |> CloudFlow.sum
    |> cluster.CreateProcess


// Check progress
nativeMathJob.ShowInfo()

cluster.ShowWorkers()

cluster.ShowProcesses()

// Wait for the result
nativeMathJob.Result

(** Now compare the execution times: *) 
let timeNative  = nativeMathJob.ExecutionTime.Value.TotalSeconds / 1000.0 
let timeManaged = managedMathJob.ExecutionTime.Value.TotalSeconds / 100.0  

timeManaged/timeNative

(** In this tutorial, you've learned how to use C# DLLs, NuGet packages and 
native DLLs in your MBrace computations. Continue with further samples to learn more about the
MBrace programming model.  *)