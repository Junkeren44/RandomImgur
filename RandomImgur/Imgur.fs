﻿module Imgur

open System
open System.ComponentModel
open System.Net
open System.Text
open System.IO
open System.Threading

let random = new Random()

let mutable proxy = null : IWebProxy

let mutable bw : BackgroundWorker = null
let mutable filter : string -> bool = Filters.empty
let mutable completed = 0

let modes = [
    ("Random", Filters.empty);
    ("jpg-only", Filters.fileType "jpg|jpeg");
    ("gif-only", Filters.fileType "gif");
    ("png-only", Filters.fileType "png");
    ("< 20 views", Filters.pViews (fun n -> n <= 20))
]

type progressReport = Failure
                    | Picture of Stream * Uri 

exception EndComputation

let generateId len =
       "0123456789ABCDEFGHIJKLMNOPQRSTUVWXTZabcdefghiklmnopqrstuvwxyz"
    |> List.replicate 5
    |> List.map (fun s -> s.Chars (random.Next s.Length))
    |> String.Concat

let rec getPicture (client : WebClient) = 
    if bw.CancellationPending then ignore (Interlocked.Decrement(&completed)) else
        let id = generateId 5
        
        let thumbUri = new Uri("http://i.imgur.com/" + id + "s.png")
        client.DownloadDataAsync(thumbUri, id)

and thumbDownloaded (sender : obj) (args:DownloadDataCompletedEventArgs) =
    if bw.CancellationPending then ignore (Interlocked.Decrement(&completed)) else
        let client = sender :?> WebClient
        let id = args.UserState :?> string

        if args.Cancelled || args.Error <> null || client.ResponseHeaders.["Content-Length"] = "503" then
            bw.ReportProgress(0, Failure)
            ignore (getPicture client)
        else
            let pageUri = new Uri("http://imgur.com/" + id)
            client.DownloadStringAsync(pageUri, (id, args.Result))

and pageDownloaded (sender : obj) (args:DownloadStringCompletedEventArgs) =
    if bw.CancellationPending then ignore (Interlocked.Decrement(&completed)) else
        let client = sender :?> WebClient
        let (id, thumbData) = args.UserState :?> string * byte[]

        if not args.Cancelled && args.Error = null && filter args.Result then
            bw.ReportProgress(0, 
                Picture (Stream.Synchronized (new MemoryStream(thumbData)), new Uri("http://i.imgur.com/" + id + ".jpg"))
            )
            ignore (Interlocked.Decrement(&completed))
        else
            bw.ReportProgress(0, Failure)
            ignore (getPicture client)

let rec loopUntilCompleted () =
    if completed = 0 then ()
    else
        Threading.Thread.Sleep(100) 
        loopUntilCompleted ()

let findPictures (sender : obj) (args : DoWorkEventArgs) =
    bw <- sender :?> BackgroundWorker
    let (count, filt) = args.Argument :?> int * (string -> bool)
    filter <- filt

    completed <- count

    let dummyClient = new WebClient()
    ignore (dummyClient.DownloadString ("http://google.com"))

    try
        ignore [| for i in 1 .. count ->
                    let client = new WebClient()
                    client.Proxy <- dummyClient.Proxy
                    client.DownloadDataCompleted.AddHandler(new DownloadDataCompletedEventHandler(thumbDownloaded))
                    client.DownloadStringCompleted.AddHandler(new DownloadStringCompletedEventHandler(pageDownloaded))
                    getPicture (client) 
        |]
    with
        | :? EndComputation -> ()

    loopUntilCompleted ()

    if bw.CancellationPending then
        args.Cancel <- true
    else
        ()

     
