local filePath = arg[1]
local toggleString = arg[2]
local toggle

if not filePath then
    print("SetStandaloneFileCommented.lua {filePath} {toggleBool (0, 1, true, false, True, False)}")
    return
end

if not toggleString then
    print("SetStandaloneFileCommented.lua {filePath} {toggleBool (0, 1, true, false, True, False)}")
    error("Must provide filePath and toggleString argument.")
    return
end

if toggleString == "0" then
    toggle = false
elseif toggleString == "1" then
    toggle = true
elseif string.lower(toggleString) == "true" then
    toggle = true
elseif string.lower(toggleString) == "false" then
    toggle = false
else
    print("SetStandaloneFileCommented.lua {filePath} {toggleBool (0, 1, true, false, True, False)}")
    error("Could not parse toggleBool argument: " .. toggleString)
end

