#include "CacheProvider.h"

#include <algorithm>
#include <cstring>
#include <chrono>

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
}

cache_provider::~cache_provider() {
	for (auto i : this->id_idx)
		delete i.second;
}

void cache_provider::lock() {
	this->mtx.lock();
	this->lock_holder = this_thread::get_id();
}

void cache_provider::unlock() {
	this->lock_holder = thread::id();
	this->mtx.unlock();
}

void cache_provider::begin_update(coord x, coord y, size width, size height) {
	this->lock();
}

void cache_provider::end_update() {
	this->unlock();
}

void cache_provider::remove(map_object& object) {
	unique_lock<recursive_mutex> lck(this->mtx);

	auto ptr = this->id_idx[object.id];

	if (ptr->last_updated_by_cache != object.last_updated_by_cache)
		throw synchronization_exception();

	auto& owner = this->owner_idx[object.owner];
	auto iter = find(owner.begin(), owner.end(), ptr);
	if (iter != owner.end())
		owner.erase(iter);

	for (coord x = object.x; x < object.x + object.width; x++)
		for (coord y = object.y; y < object.y + object.height; y++)
			this->get_loc(x, y) = nullptr;

	this->id_idx.erase(object.id);

	delete ptr;
}

void cache_provider::add(map_object& object) {
	unique_lock<recursive_mutex> lck(this->mtx);

	for (coord x = object.x; x < object.x + object.width; x++)
		for (coord y = object.y; y < object.y + object.height; y++)
			if (this->get_loc(x, y) != nullptr)
				throw synchronization_exception();

	auto new_obj = object.clone();
	this->id_idx[object.id] = new_obj;
	this->owner_idx[object.owner][object.id] = new_obj;

	for (coord x = object.x; x < object.x + object.width; x++)
		for (coord y = object.y; y < object.y + object.height; y++)
			this->get_loc(x, y) = new_obj;
}

map_object*& cache_provider::get_loc(coord x, coord y) {
	return this->loc_idx[x][y];
}

void cache_provider::clamp(coord& start_x, coord& start_y, coord& end_x, coord& end_y) {
	if (start_x < this->start_x) start_x = this->start_x;
	if (start_y < this->start_y) start_y = this->start_y;
	if (end_x >= this->end_x) end_x = this->end_x - 1;
	if (end_y >= this->end_y) end_y = this->end_y - 1;
}

unique_ptr<map_object> cache_provider::get_by_id(obj_id search_id) {
	unique_lock<recursive_mutex> lck(this->mtx);

	if (this->id_idx.count(search_id) == 0)
		return nullptr;

	return unique_ptr<map_object>(this->id_idx[search_id]->clone());
}

unique_ptr<map_object> cache_provider::get_at_location(coord x, coord y) {
	unique_lock<recursive_mutex> lck(this->mtx);

	auto obj = this->get_loc(x, y);
	if (obj)
		return unique_ptr<map_object>(obj->clone());
	else
		return unique_ptr<map_object>();
}

vector<unique_ptr<objects::map_object>> cache_provider::get_in_area(coord x, coord y, size width, size height) {
	vector<unique_ptr<objects::map_object>> result;
	coord end_x = x + width;
	coord end_y = y + height;
	
	this->clamp(x, y, end_x, end_y);
				
	unique_lock<recursive_mutex> lck(this->mtx);
	for (; x < end_x; x++) {
		for (y = end_y - height; y < end_y; y++) {
			map_object* current = this->get_loc(x, y);
			if (current)
				result.emplace_back(current->clone());
		}
	}

	return result;
}

vector<unique_ptr<map_object>> cache_provider::get_by_owner(owner_id owner) {
	vector<unique_ptr<map_object>> result;

	unique_lock<recursive_mutex> lck(this->mtx);
	if (this->owner_idx.count(owner) != 0)
		for (auto i : this->owner_idx[owner])
			result.emplace_back(i->clone());

	return result;
}

vector<unique_ptr<map_object>> cache_provider::get_in_owner_los(owner_id owner) {
	vector<unique_ptr<map_object>> result;

	unique_lock<recursive_mutex> lck(this->mtx);
	if (this->owner_idx.count(owner) == 0)
		return result;

	vector<map_object*> owner_objects = this->owner_idx[owner];
	
	coord start_x, start_y, end_x, end_y, x, y;
	for (auto current_object : owner_objects) {
		start_x = current_object->x - this->los_radius;
		start_y = current_object->y - this->los_radius;
		end_x = current_object->x + this->los_radius;
		end_y = current_object->y + this->los_radius;

		this->clamp(start_x, start_y, end_x, end_y);
		
		for (x = start_x; x < end_x; x++) {
			for (y = start_y; y < end_y; y++) {
				map_object* current_test_object = this->get_loc(x, y);
				if (current_test_object)
					result.emplace_back(current_test_object->clone());
			}
		}
	}

	return result;
}

vector<unique_ptr<map_object>> cache_provider::get_in_owner_los(owner_id owner, coord x, coord y, size width, size height) {
	vector<unique_ptr<map_object>> result;

	for (auto& current_object : this->get_in_owner_los(owner))
		if (current_object->x >= x && current_object->y >= y && current_object->x <= x + width && current_object->y <= y + height)
			result.push_back(move(current_object));

	return result;
}

vector<obj_id> cache_provider::get_users_with_los_at(coord x, coord y) {
	vector<obj_id> result;
	coord end_x = x + this->los_radius;
	coord end_y = y + this->los_radius;
	coord start_x = x - this->los_radius;
	coord start_y = y - this->los_radius;

	this->clamp(start_x, start_y, end_x, end_y);

	unique_lock<recursive_mutex> lck(this->mtx);
	for (coord this_x = start_x; this_x < end_x; this_x++) {
		for (coord this_y = start_y; this_y < end_y; this_y++) {
			map_object* current = this->get_loc(this_x, this_y);
			if (current)
				result.push_back(current->owner);
		}
	}

	return result;
}

bool cache_provider::is_area_empty(coord x, coord y, size width, size height) {
	coord end_x = x + width;
	coord end_y = y + height;

	this->clamp(x, y, end_x, end_y);

	unique_lock<recursive_mutex> lck(this->mtx);
	for (; x < end_x; x++)
		for (y = end_y - height; y < end_y; y++)
			if (this->get_loc(x, y))
				return false;

	return true;
}

bool cache_provider::is_location_in_los(coord x, coord y, owner_id owner) {
	coord end_x = x + this->los_radius;
	coord end_y = y + this->los_radius;
	coord start_x = x - this->los_radius;
	coord start_y = y - this->los_radius;

	this->clamp(start_x, start_y, end_x, end_y);

	unique_lock<recursive_mutex> lck(this->mtx);
	for (x = start_x; x < end_x; x++) {
		for (y = start_y; y < end_y; y++) {
			map_object* current = this->get_loc(x, y);
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
	unique_lock<recursive_mutex> lck(this->mtx);
	return this->owner_idx.count(user_id) != 0;
}