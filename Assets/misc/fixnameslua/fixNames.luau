local fs = require("@lune/fs")

local backup = "./MonopolyBoardBackup_0"
local finished = "./MonopolyBoardFinished"

local function recurseBackupNumber()
    if fs.isFile(backup) then
        local num = string.sub(backup, string.len(backup)-3, string.len(backup)-3)
        num = tonumber(num)
        num += 1

        if num > 9 then
            print("CANNOT CONTINUE, TOO MANY BACKUPS CREATED")
            return false
        end

        local pre = string.sub(backup, 1, string.len(backup)-4)
        pre..=tostring(num)
        backup = pre
    end

    if fs.isFile(backup) then
        return recurseBackupNumber()
    end

    return backup
end

local canContinue = recurseBackupNumber()
if not canContinue then
    return
end

local contents = fs.readFile("./MonopolyBoard.cs")
fs.writeFile(backup, contents)

local names = {
    pink = {
        "Bryce's Skunky Dungeon",
        "Fitz' FN FREEHAND",
        "Brycen's Goon Cave"
    },
    orange = {
        "Yodie-Land",
        "Section 80",
        "The Liqo Sto"
    },
    red = {
        "Landon's Room",
        "Caden's Cockhouse",
        "Lucas"
    },
    yellow = {
        "The Co-Op",
        "Mike's Place",
        "Shrine Auditorium"
    },
    green = {
        "Chance",
        "Troop",
        "Blayze's Giant Balls"
    },
    dark_blue = {
        "The Mos Eisley Cantina",
        "The Grand Casino"
    },


    tax = {
        "Plug Tax",
        "UCO Luxury Tax"
    },

    utility= {
        "Kickapoo Casino",
        "Riverwind Casino"
    },

    railroad = {
        "Season Railroad",
        "Train Railroad",
        "Cobblestone Railroad",
        "Cache Railroad"
    }
}



local keys = {}

for color, list in pairs(names) do
    if color == "tax" then continue end

    if color == "utility" then
        if not keys["utility_"] then
            keys["utility_"] = list
        end
        continue
    end

    if color == "railroad" then
        if not keys["railroad_"] then
            keys["railroad_"] = list
        end
        continue
    end

    if not keys["property_"..color.."_"] then
        keys["property_"..color.."_"] = list
    end
end

for key, list in pairs(keys) do
    for i, name in pairs(list) do
        local index = i-1
        local contentKey = key..tostring(index)
        local si, ei = string.find(contents, contentKey, 1, true)
        print(contentKey.." in contents: start: "..tostring(si).. "end: "..tostring(ei))

        if ei then
            local newlineIdx = string.find(contents, "\n", ei + 1)

            if newlineIdx then
                local nextLineStart = newlineIdx + 1

                print("Next line starts at:", nextLineStart)

                -- optional: get the actual next line
                local nextLineEnd = string.find(contents, "\n", nextLineStart) or (#contents + 1)
                local nextLine = string.sub(contents, nextLineStart, nextLineEnd - 1)

                local nlsi, nlei = string.find(nextLine, "DisplayName = ")
                if nlei then
                    local newLine = string.sub(nextLine, 1, nlei)
                        .. [["]]
                        .. name
                        .. [["]]
                        .. ","

                    contents =
                        string.sub(contents, 1, nextLineStart - 1)
                        .. newLine
                        .. string.sub(contents, nextLineEnd)
                end
            end
        end
    end
end

fs.writeFile(finished, contents)

--[[
pink:


orange:


red:


yellow:


green:


dark_blue:

]]