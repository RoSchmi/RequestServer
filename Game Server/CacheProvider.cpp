#include "CacheProvider.h"

#include <algorithm>
#include <cstring>

using namespace std;
using namespace util;
using namespace util::sql;
using namespace game_server;
using namespace game_server::objects;

cache_provider::cache_provider(coord start_x, coord start_y, size width, size height, size los_radius) {
	this->start_x = start_x;
	this->start_y = start_y;
	this->end_x = start_x + width;
	this->end_y = start_y + height;
	this->width = width;
	this->height = height;
	this->los_radius = los_radius;
	this->loc_idx = new map_object*[width * height];
	memset(this->loc_idx, 0, width * height * sizeof(map_object*));
}

cache_provider::~cache_provider() {
	delete [] this->loc_idx;

	for (auto i : this->id_idx)
		delete i.second;
}

void cache_provider::remove(data_object* object) {
	this->id_idx.erase(object->id);
	this->owner_idx[object->owner].erase(object->id);
}

void cache_provider::remove(map_object* object) {
	this->remove(static_cast<data_object*>(object));

	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->get_by_location(x, y) = nullptr;
}

void cache_provider::add(data_object* object) {
	this->id_idx[object->id] = object;
	this->owner_idx[object->owner][object->id] = object;
}

void cache_provider::add(map_object* object) {
	this->add(static_cast<data_object*>(object));

	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->get_by_location(x, y) = object;
}

void cache_provider::update_location(map_object* object, coord new_x, coord new_y) {
	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->get_by_location(x, y) = nullptr;

	object->x = new_x;
	object->y = new_y;

	for (coord x = object->x; x < object->x + object->width; x++)
		for (coord y = object->y; y < object->y + object->height; y++)
			this->get_by_location(x, y) = object;
}

void cache_provider::clamp(coord& start_x, coord& start_y, coord& end_x, coord& end_y) {
	if (start_x < this->start_x) start_x = this->start_x;
	if (start_y < this->start_y) start_y = this->start_y;
	if (end_x >= this->end_x) end_x = this->end_x - 1;
	if (end_y >= this->end_y) end_y = this->end_y - 1;
}

data_object* cache_provider::get_by_id(obj_id search_id) {
	if (this->id_idx.count(search_id) == 0)
		return nullptr;

	return this->id_idx[search_id];
}

map_object*& cache_provider::get_by_location(coord x, coord y) {
	return this->loc_idx[this->width * static_cast<obj_id>(y - this->start_y) + static_cast<obj_id>(x - this->start_x)];
}

unordered_set<map_object*> cache_provider::get_in_area(coord x, coord y, size width, size height) {
	unordered_set<map_object*> result;
	coord end_x = x + width;
	coord end_y = y + height;

	this->clamp(x, y, end_x, end_y);
				
	for (; x < end_x; x++) {
		for (y = end_y - height; y < end_y; y++) {
			map_object* current = this->get_by_location(x, y);
			if (current)
				result.insert(current);
		}
	}

	return result;
}

bool cache_provider::is_area_empty(coord x, coord y, size width, size height) {
	coord end_x = x + width;
	coord end_y = y + height;

	this->clamp(x, y, end_x, end_y);
	
	for (; x < end_x; x++)
		for (y = end_y - height; y < end_y; y++)
			if (this->get_by_location(x, y))
				return false;

	return true;
}

bool cache_provider::is_location_in_los(coord x, coord y, owner_id owner) {
	coord end_x = x + this->los_radius;
	coord end_y = y + this->los_radius;
	coord start_x = x - this->los_radius;
	coord start_y = y - this->los_radius;

	this->clamp(start_x, start_y, end_x, end_y);

	for (x = start_x; x < end_x; x++) {
		for (y = start_y; y < end_y; y++) {
			map_object* current = this->get_by_location(x, y);
			if (current && current->owner == owner) {
				return true;
			}
		}
	}

	return false;
}

bool cache_provider::is_location_in_bounds(coord x, coord y, size width, size height) {
	return x >= this->start_x && y >= this->start_y && x + width <= this->end_x && y + height <= this->end_y;
}

bool cache_provider::is_user_present(obj_id user_id) {
	return this->id_idx.count(user_id) != 0;
}

const unordered_map<obj_id, data_object*> cache_provider::get_by_owner(owner_id owner) {
	return this->owner_idx[owner];
}

unordered_set<map_object*> cache_provider::get_in_owner_los(owner_id owner) {
	unordered_map<obj_id, data_object*> ownerObjects = this->owner_idx[owner];
	
	coord start_x, start_y, end_x, end_y, x, y;
	unordered_set<map_object*> result;
	for (auto i : ownerObjects) {
		map_object* currentOwnerObject = dynamic_cast<map_object*>(i.second);
		if (!currentOwnerObject) 
			continue;

		start_x = currentOwnerObject->x - this->los_radius;
		start_y = currentOwnerObject->y - this->los_radius;
		end_x = currentOwnerObject->x + this->los_radius;
		end_y = currentOwnerObject->y + this->los_radius;

		this->clamp(start_x, start_y, end_x, end_y);

		for (x = start_x; x < end_x; x++) {
			for (y = start_y; y < end_y; y++) {
				map_object* currentTestObject = this->get_by_location(x, y);
				if (currentTestObject)
					result.insert(currentTestObject);
			}
		}
	}

	return result;
}

unordered_set<map_object*> cache_provider::get_in_owner_los(owner_id owner, coord x, coord y, size width, size height) {
	unordered_set<map_object*> result;

	for (auto i : this->get_in_owner_los(owner)) {
		map_object* currentObject = dynamic_cast<map_object*>(i);
		if (!currentObject)
			continue;

		if (currentObject->x >= x && currentObject->y >= y && currentObject->x <= x + width && currentObject->y <= y + height)
			result.insert(currentObject);
	}

	return result;
}

unordered_set<obj_id> cache_provider::get_users_with_los_at(coord x, coord y) {
	unordered_set<obj_id> result;
	coord end_x = x + this->los_radius;
	coord end_y = y + this->los_radius;
	coord start_x = x - this->los_radius;
	coord start_y = y - this->los_radius;

	this->clamp(start_x, start_y, end_x, end_y);
				
	for (coord this_x = start_x; this_x < end_x; this_x++) {
		for (coord this_y = start_y; this_y < end_y; this_y++) {
			map_object* current = this->get_by_location(this_x, this_y);
			if (current)
				result.insert(current->owner);
		}
	}

	return result;
}