﻿module Settings

open System.Configuration

type SettingType = BooleanSetting | IntegerSetting

let settings = [
    ("Use proxy", "UseProxy", BooleanSetting);
    ("Check for updates", "CheckForUpdates", BooleanSetting);
    ("Number of images", "NumPics", IntegerSetting);
    ("Number of connections", "NumThreads", IntegerSetting);
    ("Use old IDs?", "OldIdLength", BooleanSetting);
]

type public Settings() =
    inherit ApplicationSettingsBase()

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("false")>]
    member this.UseProxy
        with get() = this.Item("UseProxy") :?> bool
        and set(value : bool) = this.Item("UseProxy") <- value

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("true")>]
    member this.CheckForUpdates
        with get() = this.Item("CheckForUpdates") :?> bool
        and set(value : bool) = this.Item("CheckForUpdates") <- value

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("100")>]
    member this.NumPics
        with get() = this.Item("NumPics") :?> int
        and set(value : int) = this.Item("NumPics") <- value

    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("true")>]
    member this.OldIdLength
        with get() = this.Item("OldIdLength") :?> bool
        and set(value : bool) = this.Item("OldIdLength") <- value


    [<UserScopedSettingAttribute()>]
    [<DefaultSettingValueAttribute("20")>]
    member this.NumThreads
        with get() = this.Item("NumThreads") :?> int
        and set(value : int) = this.Item("NumThreads") <- value