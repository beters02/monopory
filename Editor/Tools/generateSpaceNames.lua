local str = [[
| 0 | `go` | Landing |
| 1 | `property_brown_0` | Studio Apartment |
| 2 | `chest_0` | Community Chest |
| 3 | `property_brown_1` | Laundry Lofts |
| 4 | `tax_income` | Plug Tax |
| 5 | `railroad_0` | Transit Hub |
| 6 | `property_light_blue_0` | College Commons |
| 7 | `chance_0` | Chance |
| 8 | `property_light_blue_1` | de_Miraq |
| 9 | `property_light_blue_2` | de_Nuke |
| 10 | `jail` | Visiting Eviction Court |
| 11 | `property_pink_0` | Downtown District |
| 12 | `utility_0` | Power Grid |
| 13 | `property_pink_1` | Market Square |
| 14 | `property_pink_2` | Canals District |
| 15 | `railroad_1` | Metro Line |
| 16 | `property_orange_0` | Riverside Villas |
| 17 | `chest_1` | Community Chest |
| 18 | `property_orange_1` | Harbor 17 |
| 19 | `property_orange_2` | Skyline Towers |
| 20 | `free_parking` | Free Parking |
| 21 | `property_red_0` | Black Mesa Business Park |
| 22 | `chance_1` | Chance |
| 23 | `property_red_1` | Lambda Square |
| 24 | `property_red_2` | Ravenholm Heights |
| 25 | `railroad_2` | Express Line |
| 26 | `property_yellow_0` | Kleiner Commons |
| 27 | `property_yellow_1` | White Forest Estates |
| 28 | `utility_1` | Internet Provider |
| 29 | `property_yellow_2` | Vertigo Towers |
| 30 | `go_to_jail` | Evicted! |
| 31 | `property_green_0` | Construct Court |
| 32 | `property_green_1` | City 17 Condos |
| 33 | `chest_2` | Community Chest |
| 34 | `property_green_2` | Nova Prospekt Villas |
| 35 | `railroad_3` | Rapid Transit |
| 36 | `chance_2` | Chance |
| 37 | `property_dark_blue_0` | Facepunch Plaza |
| 38 | `tax_luxury` | HOA Fine |
| 39 | `property_dark_blue_1` | Billionaire Boulevard |
]]

local counter = -1

local writeStr = ""

for id in string.gmatch(str, "|%s*%d+%s*|%s*`([^`]+)`") do
    counter = counter + 1
    writeStr = writeStr .. '"' .. tostring(counter) .. '_' .. id .. '",' .. "\n"
end

-- 1. Open the file in write mode
local file, err = io.open("generatedSpaceNames.txt", "w")

if not file then
    print("Error opening file: " .. err)
else
    -- 2. Write the string to the file
    file:write(writeStr)
    
    -- 3. Close the file to save changes
    file:close()
end
