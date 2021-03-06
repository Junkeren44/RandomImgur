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
let mutable completed : int64 = (int64) 0
let mutable webClients : WebClient[] = null
let mutable settings = new Settings.Settings()

let modes = [
    ("Random", Filters.empty);
    ("jpg-only", Filters.fileType "jpg|jpeg");
    ("gif-only", Filters.fileType "gif");
    ("png-only", Filters.fileType "png");
    ("< 10 views", Filters.pViews (fun n -> n <= 10))
    ("> 1000 views", Filters.pViews (fun n -> n >= 1000))
]

type progressReport = Failure
                    | Picture of Stream * Uri 

exception EndComputation

let generateId len =
       "0123456789ABCDEFGHIJKLMNOPQRSTUVWXTZabcdefghiklmnopqrstuvwxyz"
    |> List.replicate len
    |> List.map (fun s -> s.Chars (random.Next s.Length))
    |> String.Concat

let rec getPicture left (client : WebClient) = 
    if bw.CancellationPending || !left = 0 then ignore (Interlocked.Decrement(&completed)) else
        let id = generateId (if settings.OldIdLength then 5 else 7)
        
        let thumbUri = new Uri("http://i.imgur.com/" + id + "s.png")
        client.DownloadDataAsync(thumbUri, id)

and thumbDownloaded left (sender : obj) (args:DownloadDataCompletedEventArgs) =
    if bw.CancellationPending || args.Cancelled then ignore (Interlocked.Decrement(&completed)) else
        let client = sender :?> WebClient
        let id = args.UserState :?> string

        if args.Cancelled || args.Error <> null || client.ResponseHeaders.["Content-Length"] = "503" then
            bw.ReportProgress(0, Failure)
            ignore (getPicture left client)
        else
            let pageUri = new Uri("http://imgur.com/" + id)
            client.DownloadStringAsync(pageUri, (id, args.Result))

and pageDownloaded left (sender : obj) (args:DownloadStringCompletedEventArgs) =
    if bw.CancellationPending || args.Cancelled then ignore (Interlocked.Decrement(&completed)) else
        let client = sender :?> WebClient
        let (id, thumbData) = args.UserState :?> string * byte[]

        if not args.Cancelled && args.Error = null && (try filter args.Result with | _ -> false) then
            bw.ReportProgress(0, 
                Picture (Stream.Synchronized (new MemoryStream(thumbData)), new Uri("http://i.imgur.com/" + id + ".jpg"))
            )
            left := !left - 1
        else
            bw.ReportProgress(0, Failure)
        
        ignore (getPicture left client)

let rec loopUntilCompleted () =
    if Interlocked.Read(&completed) = (int64)0 then ()
    else
        if bw.CancellationPending then
            Array.iter (fun (wc:WebClient) -> wc.CancelAsync ()) webClients
            webClients <- [| |]
        else
            ()

        Threading.Thread.Sleep(100) 
        loopUntilCompleted ()

let findPictures (sender : obj) (args : DoWorkEventArgs) =
    bw <- sender :?> BackgroundWorker
    let (count, filt) = args.Argument :?> int * (string -> bool)
    filter <- filt

    ignore (Interlocked.Exchange(&completed, (int64)settings.NumThreads))

    settings <- new Settings.Settings()

    let proxy = (
        if settings.UseProxy then
            let dummyClient = new WebClient()
            ignore (dummyClient.DownloadString ("http://google.com"))
            dummyClient.Proxy
        else null
    )

    let rem = count % settings.NumThreads;
    webClients <- [|
        for i in 1 .. settings.NumThreads ->
            let left = ref (count / settings.NumThreads + if i <= rem then 1 else 0)
            let client = new WebClient()
            client.Proxy <- proxy
            client.DownloadDataCompleted.AddHandler(new DownloadDataCompletedEventHandler(thumbDownloaded left))
            client.DownloadStringCompleted.AddHandler(new DownloadStringCompletedEventHandler(pageDownloaded left))
            getPicture left client
                    
            client 
    |]

    loopUntilCompleted ()

    if bw.CancellationPending then
        args.Cancel <- true
    else
        ()

     

